using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Files;
using EmbedIO.Actions;
using EmbedIO.Cors;

namespace BizDeck {
    public class Server {
        // ButtonAction subclasses include AppButton for app launches, StepsButton for
        // playing Chrome DevTools Recorder scipts, and ActionsButton for running
        // BizDeck action scripts. Each of those three button types includes a driver
        // to launch the app, play the recorder step script, or execute the actions.
        private Dictionary<string, ButtonAction> button_action_map = new Dictionary<string, ButtonAction>();
        private object button_action_map_lock = new object();

        // ConnectedDeck owns the HID connection to the StreamDeck
        private ConnectedDeck stream_deck = null;

        // ConfigHelper loads cfg/config.json, secrets.json and also provides a
        // lot of convenience helper methods
        private ConfigHelper config_helper = null;

        // Specialisation of Embedio websock module for GUI to server comms
        // to populate GUI tabs, including Config, Status, Cache.
        private BizDeckWebSockModule websock = null;

        // BizDeck logger adds threadIDs to Embedio's Swan logger. Very handy
        // for debugging deadlocks caused by async code that fails to catch
        // exceptions. For example, if you don't catch PuppeteerSharp exceptions
        // thrown when playing a recorder step script, you'll get deadlocks.
        private BizDeckLogger logger;

        // Main Embedio server object for handling HTTP GETs from GUI
        // as well as REST API
        private IWebServer http_server;

        // DeckManager creates the ConnectedDeck, and knows about the different
        // Elgato hardware that may be connected.
        private DeckManager deck_manager;

        // Used to exit the main Task.WaitAll
        private CancellationTokenSource server_exit_token;

        private Timer blink_timer;

        // Use of Lazy<T> gives us a thread safe singleton
        // Instance property is the access point
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<Server> lazy =
            new Lazy<Server>(() => new Server());
        public static Server Instance { get { return lazy.Value; } }

        private Server() {
            logger = new(this);
            config_helper = ConfigHelper.Instance;
            // One shot copy so that hx gui can get default background from
            // local cache avoiding hx hardwiring and using a single source of truth 
            BizDeckStatus.Instance.StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            // We put the background default in status to make it easy
            // for the AddButton dialog to display the default value. The alternative
            // would be special case handling in on_config(), which would make
            // that code far less generic.
            BizDeckStatus.Instance.BackgroundDefault = config_helper.BizDeckConfig.BackgroundDefault;
            // Slider changes status.Brightness dynamically, but it only gets
            // written back to config when the user hits the apply button
            BizDeckStatus.Instance.Brightness = config_helper.BizDeckConfig.DeckBrightnessPercentage;
            BizDeckStatus.Instance.MyURL = $"http://{config_helper.BizDeckConfig.HTTPHostName}:{config_helper.BizDeckConfig.HTTPServerPort}";
            // Create our IronPython executor
            BizDeckPython.Instance.Init(config_helper);
            // Create websock here so that ConnectStreamDeck and CreateWebServer can get from
            // the member var, and we can pass it to button actions enabling them to send
            // notifications to the GUI on fails
            websock = new BizDeckWebSockModule(config_helper);
            // websock needs to know about the server object
            // so it can dispatch 
            websock.MainServerObject = this;
            // First, let's connect to the StreamDeck
            ConnectStreamDeck();
            // Now we have an IconCache we can invoke InitDeck, which
            // will pull StreamDeck compatible JPEGS from the cache
            // which have been sized correctly for the deck buttons.
            if (stream_deck != null) {
                stream_deck.InitDeck();
            }
            // Now we have my_url, stream_deck, websock members set we can
            // build the button action map
            RebuildButtonActionMap();
            // Instance and plug together Embedio server objects
            http_server = CreateWebServer();
            // Create the timer object for blinking buttons
            blink_timer = new Timer(this.BlinkTimerCallback, null, 5000, 
                                config_helper.BizDeckConfig.BlinkInterval);
        }

        protected bool ConnectStreamDeck() {
            // TODO: make this reentrant, so a bounce is not necessary to handle
            // the "deck not plugged in scenario". Will likely need some cleaning
            // up on points of coupling with ConfigHelper and BizDeckWebSockModule.
            // We'll probably have to turn the Run method into a loop so the 
            // Task.WaitAll(<tasks>) can be rebuilt. Will need a cancel token
            // that can be triggered by a GUI event to make the web task complete
            // and allow the loop to rebuild tasks.
            deck_manager = new DeckManager(config_helper);
            stream_deck = deck_manager.SetupDeck(this);
            if (stream_deck == null) {
                config_helper.ThrowErrorToBrowser("StreamDeck init", "Is your StreamDeck plugged in?");
                logger.Error("StreamDeck init failed - is it plugged in?");
                BizDeckStatus.Instance.DeckConnection = false;
                return false;
            }
            // Now we're connected to the deck update status with ButtonSize and Count
            // We must do that before we invoke ConnectedDeck.InitDeck, which calls
            // ConnectedDeck.SetupDeviceButtons, which uses the IconCache. And the
            // IconCache depends on ButtonSize.
            BizDeckStatus.Instance.DeckConnection = true;
            BizDeckStatus.Instance.ButtonCount = stream_deck.ButtonCount;
            BizDeckStatus.Instance.ButtonSize = stream_deck.ButtonSize;
            BizDeckStatus.Instance.DeviceName = stream_deck.Name;
            // Let the websock module know about the stream deck
            // so it can resend buttons as necessary
            websock.StreamDeck = stream_deck;
            return true;
        }

        // The "main" method that kicks off the stream deck and http server
        // async read handler processes
        public void Run() { 
            var http_server_task = http_server.RunAsync();
            http_server_task.ConfigureAwait(false);
            Task stream_deck_task;
            if (stream_deck == null) {
                // StreamDeck init failed - maybe not plugged in?
                stream_deck_task = Task.CompletedTask;
            }
            else {
                stream_deck_task = stream_deck.ReadAsync();
                stream_deck_task.ConfigureAwait(false);
            }
            server_exit_token = new CancellationTokenSource();
            // Blocks this main thread waiting on the two tasks
            Task[] server_tasks = { http_server_task, stream_deck_task };
            try {
                Task.WaitAll(server_tasks, server_exit_token.Token);
            }
            catch (System.OperationCanceledException ex) {
                logger.Info($"Run: Task.WaitAll() cancelled: {ex.Message}");
            }
            catch (Exception ex) {
                logger.Info($"Run: Task.WaitAll() {ex}");
            }
        }


        public void Shutdown() {
            if (server_exit_token == null) {
                logger.Error("Shutdown: no server_exit_token");
                return;
            }
            logger.Fatal("Shutting down...");
            server_exit_token.Cancel();
        }


        private WebServer CreateWebServer() {
            // NB the static_module is added last below so it's the last candidate match,
            // otherwise it would match all GETs
            var excel_module = new ActionModule("/excel", HttpVerbs.Get, ExcelCallback);
            var static_module = new ActionModule("/", HttpVerbs.Any, 
                                    ctx => ctx.SendDataAsync(new { Message = "Error" }));
            // Excel web queries do an HTTP OPTIONS request before doing the first
            // HTTP GET to a new endpoint. The CORS module handles that request and
            // says OK back to Excel.
            var cors_module = new CorsModule("/");

            var server = new WebServer(o => o.WithUrlPrefix(BizDeckStatus.Instance.MyURL)
                .WithMode(HttpListenerMode.EmbedIO))
                // configure our web server by adding modules.
                .WithModule(cors_module)
                .WithLocalSessionManager()
                .WithModule(websock)
                .WithModule(excel_module)
                .WithWebApi("/api", m => m.WithController( ApiControllerFactory))
                .WithStaticFolder("/icons", config_helper.IconsDir, false, m => m.WithoutContentCaching())
                .WithStaticFolder("/", config_helper.HtmlDir, true, m => m.WithContentCaching())
                .WithModule(static_module);

            // Listen for state changes.
            server.StateChanged += (s, e) => logger.Info($"StateChanged: NewState[{e.NewState}]");
            return server;
        }

        public void SetDeckBrightness(int brightness) {
            if (stream_deck == null) {
                logger.Error($"SetDeckBrightness: {brightness}% - deck not connected");
                return;
            }
            stream_deck.SetBrightness(brightness);
        }

        public BizDeckResult RebuildButtonMaps() {
            // This will trigger ConnectedDeck.SetupDeviceButtons(), which sets
            // the button images defined in BizDeckConfig.ButtonList, and also
            // does ClearKey when a button has been deleted. We need to do that
            // before rebuilding the action map because ConnectedDeck.SetupDeviceButtons()
            // relies on the length difference between the ButtonDefnList and
            // ButtonActionMap to figure out which buttons need clearing.
            if (stream_deck != null) {
                stream_deck.SetupDeviceButtons();
            }
            else {
                return BizDeckResult.StreamDeckNotConnected;
            }
            return RebuildButtonActionMap();
        }

        private BizDeckResult RebuildButtonActionMap( )  {
            bool rebuild_ok = true;
            string error = null;
            lock (button_action_map_lock) {
                button_action_map.Clear();
                button_action_map["page"] = new Pager(stream_deck);
                button_action_map["gui"] = new ShowBizDeckGUI(BizDeckStatus.Instance.MyURL);
                lock (config_helper.ButtonListLock) { 
                    foreach (ButtonDefinition bd in config_helper.BizDeckConfig.ButtonList) {
                        if (!button_action_map.ContainsKey(bd.Name)) {
                            switch (bd.Action) {
                                case ButtonImplType.Actions:
                                    button_action_map[bd.Name] = new ActionsButton(bd.Name, websock);
                                    break;
                                case ButtonImplType.Steps:
                                    button_action_map[bd.Name] = new StepsButton(bd.Name);
                                    break;
                                case ButtonImplType.Apps:
                                    button_action_map[bd.Name] = new AppButton(bd.Name, websock);
                                    break;
                                default:
                                    error = "unknown action[{bd.Action}] for button[{bd.Name}]";
                                    logger.Error($"RebuildButtonActionMap: {error}");
                                    rebuild_ok = false;
                                    break;
                            }
                        }
                    }
                }
            }
            return new BizDeckResult(rebuild_ok, error);
        }

        public ButtonAction GetButtonAction(string name) {
            ButtonAction ba = null;
            lock (button_action_map_lock) {
                if (button_action_map.ContainsKey(name)) {
                    return button_action_map[name];
                }
            }
            return ba;
        }

        // Convenience method to allow code that's not handling a websock event, so
        // doesn't have a context, to send messages to the GUI. For example, the
        // ConnectedDeck code that invokes RunAync on ButtonActionMap entries.
        public async Task SendNotification(string title, string body, bool fade=false) {
            await websock.SendNotification(null, title, body, fade);
        }

        public BizDeckApiController ApiControllerFactory() {
            return new BizDeckApiController(config_helper);
        }

        public async Task ExcelCallback(IHttpContext ctx) {
            // Segments will be eg
            // [0]: '/'
            // [1]: 'excel/'
            // [2]: 'quandl/'
            // [3]: 'yield.csv'
            CacheEntry cache_entry = null;
            if (ctx.Request.Url.Segments.Length < 4) {
                // When Excel is given a we query for eg http://localhost:9271/excel/quandl/yield_csv
                // via .iqy file, then for some reason it will hit http://localhost:9271/excel/quandl/
                // first, so we have to respond quick with a 404.
                logger.Error($"ExcelCallback: not enough URL segments: {ctx.Request.RawUrl}");
                // Now fall through to the using clause below with a null CacheEntry,
                // which will create a NoData page
            }
            else {
                string group = ctx.Request.Url.Segments[2].Trim('/');
                string key = ctx.Request.Url.Segments[3];
                cache_entry = DataCache.Instance.GetCacheEntry(group, key);
            }
            using (var stream = ctx.OpenResponseStream()) {
                // CacheEntryToStream will send a NoData table header
                // if we give it a null cache_entry. Allow resumption on
                // another thread as this is really simple streaming HTML
                // output, and we want to free the embedio request handler
                // thread for another incoming request.
                await HTMLHelpers.CacheEntryToStream(logger, cache_entry, stream).ConfigureAwait(false);
                // Set the status and flush output buffers so Excel knows
                // the reponse is complete. Necessary as we're async streaming.
                // BizDeckApiController doesn't need to do that as it's returning
                // complete result strings, so embedio can complete the request
                // without any further help from app code here.
                stream.Flush();
                stream.Close();
            }
        }

        private void BlinkTimerCallback(object state) {
            if (stream_deck != null) {
                stream_deck.BlinkDeviceButtons();
            }
        }
    }
}