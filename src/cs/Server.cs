using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Files;
using EmbedIO.Actions;
using CommandLine;

namespace BizDeck {
    public class Server {
        private Dictionary<string, ButtonAction> button_action_map = new Dictionary<string, ButtonAction>();
        private ConnectedDeck stream_deck = null;
        private ConfigHelper config_helper = null;
        private BizDeckWebSockModule websock = null;
        private BizDeckLogger logger;
        private IWebServer http_server;
        private DeckManager deck_manager;
        private BizDeckStatus status = new();

        public Server(ConfigHelper ch) {
            logger = new(this);
            config_helper = ch;
            // One shot copy so that hx gui can get default background from
            // local cache avoiding hx hardwiring and using a single source of truth 
            status.StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            status.BackgroundDefault = config_helper.BizDeckConfig.BackgroundDefault;
            status.MyURL = $"http://{ch.BizDeckConfig.HTTPHostName}:{ch.BizDeckConfig.HTTPServerPort}";
            // Create websock here so that ConnectStreamDeck and CreateWebServer can get from
            // the member var, and we can pass it to button actions enabling them to send
            // notifications to the GUI on fails
            websock = new BizDeckWebSockModule(config_helper, status);
            // First, let's connect to the StreamDeck
            ConnectStreamDeck();
            // Now the deck is connected we know the ButtonSize, so we
            // can construct the IconCache
            status.IconCache = new IconCache(config_helper, status);
            // Now we have an IconCache we can invoke InitDeck, which
            // will pull StreamDeck compatible JPEGS from the cache
            // which have been sized correctly for the deck buttons.
            stream_deck.InitDeck(status.IconCache);
            // Now we have my_url, stream_deck, websock members set we can
            // build the button action map
            RebuildButtonActionMap();
            // Instance and plug together Embedio server objects
            http_server = CreateWebServer();
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
            stream_deck = deck_manager.SetupDeck();
            if (stream_deck == null) {
                config_helper.ThrowErrorToBrowser("StreamDeck init", "Is your StreamDeck plugged in?");
                logger.Error("StreamDeck init failed - is it plugged in?");
                status.DeckConnection = false;
                return false;
            }
            // Now we're connected to the deck update status with ButtonSize and Count
            // We must do that before we invoke ConnectedDeck.InitDeck, which calls
            // ConnectedDeck.SetupDeviceButtons, which uses the IconCache. And the
            // IconCache depends on ButtonSize.
            status.DeckConnection = true;
            status.ButtonCount = stream_deck.ButtonCount;
            status.ButtonSize = stream_deck.ButtonSize;
            status.DeviceName = stream_deck.Name;
            // Let the websock module know about the stream deck
            // so it can resend buttons as necessary
            websock.StreamDeck = stream_deck;
            // Ditto this server object so it can rebuild
            // button action map
            websock.MainServerObject = this;
            return true;
        }

        // The "main" method that kicks off the stream deck and http server
        // async read handler processes
        public void Run() { 
            var http_server_task = http_server.RunAsync();
            http_server_task.ConfigureAwait(false);
            // TODO: embedio boilerplate had this exit_signal,
            // so we must figure how to do a clean exit...
            var exit_signal = new ManualResetEvent(false);
            Task stream_deck_task;
            if (stream_deck == null) {
                // StreamDeck init failed - maybe not plugged in?
                stream_deck_task = Task.CompletedTask;
            }
            else {

                stream_deck_task = stream_deck.ReadAsync();
                stream_deck_task.ConfigureAwait(false);
            }
            // Blocks this main thread waiting on the two tasks
            Task.WaitAll(http_server_task, stream_deck_task);
        }

        private WebServer CreateWebServer() {
            var server = new WebServer(o => o
                    .WithUrlPrefix(status.MyURL)
                    .WithMode(HttpListenerMode.EmbedIO))
                // First, we will configure our web server by adding Modules.
                .WithLocalSessionManager()
                .WithModule(websock)
                .WithStaticFolder("/icons", config_helper.IconsDir, false, m => m.WithoutContentCaching())
                .WithStaticFolder("/", config_helper.HtmlDir, true, m => m.WithContentCaching())
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            // Listen for state changes.
            server.StateChanged += (s, e) => logger.Info($"StateChanged: NewState[{e.NewState}]");
            return server;
        }

        public void RebuildButtonMaps() {
            // This will trigger ConnectedDeck.SetupDeviceButtons(), which sets
            // the button images defined in BizDeckConfig.ButtonList, and also
            // does ClearKey when a button has been deleted. We need to do that
            // before rebuilding the action map because ConnectedDeck.SetupDeviceButtons()
            // relies on the length difference between the ButtonDefnList and
            // ButtonActionMap to figure out which buttons need clearing.
            stream_deck.SetupDeviceButtons();
            RebuildButtonActionMap();
        }

        private void RebuildButtonActionMap( )  {
            button_action_map.Clear();
            button_action_map["page"] = new Pager(stream_deck);
            button_action_map["gui"] = new ShowBizDeckGUI(status.MyURL);
            // Buttons for the dev tools recorder, which we're not using currently.
            // button_action_map["start_recording"] = new StartRecording(recorder);
            // button_action_map["stop_recording"] = new StopRecording(recorder);
            foreach (ButtonDefinition bd in config_helper.BizDeckConfig.ButtonList)
            {
                if (!button_action_map.ContainsKey(bd.Name)) {
                    switch (bd.Action) {
                        case "steps":
                            button_action_map[bd.Name] = new StepsButton(config_helper, bd.Name);
                            break;
                        case "app":
                            button_action_map[bd.Name] = new AppButton(config_helper, bd.Name, websock);
                            break;
                        default:
                            logger.Info($"RebuildButtonActionMap: unknown action[{bd.Action}] for button[{bd.Name}]");
                            break;
                    }
                }
            }
            // Let the ConnectedDeck know about the button action map so it can
            // fire the ButtonAction.RunAsync implementations
            if (stream_deck != null) {
                stream_deck.ButtonActionMap = button_action_map;
            }
            else {
                logger.Error($"RebuildButtonActionMap: no StreamDeck connection");
            }
        }
    }
}