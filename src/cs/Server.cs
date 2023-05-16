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
        private string my_url;

        public Server(ConfigHelper ch) {
            logger = new(this);
            config_helper = ch;
            // TODO: config the "localhost" part of URL.
            my_url = string.Format("http://localhost:{0}/", ch.BizDeckConfig.HTTPServerPort);
            // Create websock here so that ConnectStreamDeck and CreateWebServer can get from
            // the member var, and we can pass it to button actions enabling them to send
            // notifications to the GUI on fails
            websock = new BizDeckWebSockModule(config_helper);
            // First, let's connect to the StreamDeck
            ConnectStreamDeck();
            // Now we have my_url, stream_deck, websock members set we can
            // build the button action map
            RebuildButtonActionMap();
            // Instance and plug together Embedio server objects
            http_server = CreateWebServer();
        }

        protected bool ConnectStreamDeck() {
            deck_manager = new DeckManager(config_helper);
            stream_deck = deck_manager.SetupDeck();
            if (stream_deck == null) {
                config_helper.ThrowErrorToBrowser("StreamDeck init", "Is your StreamDeck plugged in?");
                logger.Error("StreamDeck init failed - is it plugged in?");
                return false;
            }
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
                    .WithUrlPrefix(my_url)
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
            stream_deck.ButtonDefnList = config_helper.BizDeckConfig.ButtonList;
            RebuildButtonActionMap();
        }

        private void RebuildButtonActionMap( )  {
            button_action_map.Clear();
            button_action_map["page"] = new Pager(stream_deck);
            button_action_map["gui"] = new ShowBizDeckGUI(my_url);
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