namespace BizDeck
{
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

    class Program
    {
        private const bool UseFileCache = true;
        private static Dictionary<string, ButtonAction> button_action_map = new Dictionary<string, ButtonAction>();
        private static ConnectedDevice stream_deck = null;
        private static ConfigHelper config_helper = null;
        private static BizDeckWebSockModule websock = null;
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments.</param>
        static void Main(string[] args)
        {
            // First parse cmd line opts...
            var parser = new Parser();
            var result = parser.ParseArguments<CmdLineOptions>(args);
            // first load the config
            config_helper = new ConfigHelper(result.Value);
            var config = config_helper.LoadConfig();
            var url = string.Format("http://localhost:{0}/", config.HTTPServerPort);
            if (config.Console)
            {
                Win32.AllocConsole();
            }

            // var recorder = new DevToolsRecorder(config_helper);
            BizDeckLogger.InitLogging(config_helper);
            var logger = new BizDeckLogger(typeof(Program));
            // Create websock here so that CreateWebServer can get from
            // the static member var, and we can pass it to button actions
            // enabling them to send notifications to the GUI on fails
            websock = new BizDeckWebSockModule(config_helper);
            // Our web server is disposable.
            using (var server = CreateWebServer(url, config_helper, logger))  {
                // Once we've registered our modules and configured them, we
                // call the RunAsync() method.
                var http_server_task = server.RunAsync();
                http_server_task.ConfigureAwait(false);
                var exitSignal = new ManualResetEvent(false);
                // Now connect to StreamDeck, and start async read
                var device_manager = new DeviceManager(config_helper);
                stream_deck = device_manager.SetupDevice();
                if (stream_deck == null)
                {
                    logger.Error("StreamDeck init failed - is it plugged in?");
                    // TODO: figure out how to do a pending awaitable...
                    // await websock.SendNotification(null, "StreamDeck Init Failure", "Is it plugged in?");
                }
                else
                {
                    // Let the websock module know about the stream deck
                    // so it can resend buttons as necessary
                    websock.StreamDeck = stream_deck;
                    InitButtonActionMap(url, logger);
                    stream_deck.ButtonMap = button_action_map;
                    var stream_deck_task = stream_deck.ReadAsync();
                    stream_deck_task.ConfigureAwait(false);
                    // Blocks this main thread waiting on the two tasks
                    Task.WaitAll(http_server_task, stream_deck_task);
                }
                // Wait for any key to be pressed before disposing of our web server.
                // In a service, we'd manage the lifecycle of our web server using
                // something like a BackgroundWorker or a ManualResetEvent.
                if (config.Console)
                {
                    Console.ReadKey(true);
                }
            }
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string url, ConfigHelper ch, BizDeckLogger logger)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                // First, we will configure our web server by adding Modules.
                .WithLocalSessionManager()
                .WithModule(websock)
                .WithStaticFolder("/", ch.HtmlDir, true, m => m
                    .WithContentCaching(UseFileCache)) // Add static files after other modules to avoid conflicts
                .WithModule(new ActionModule("/", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })));

            // Listen for state changes.
            server.StateChanged += (s, e) => logger.Info($"StateChanged: NewState[{e.NewState}]");
            return server;
        }

        private static void InitButtonActionMap(string biz_deck_gui_url, BizDeckLogger logger)
        {
            button_action_map["page"] = new Pager(stream_deck);
            button_action_map["gui"] = new ShowBizDeckGUI(biz_deck_gui_url);
            // button_action_map["start_recording"] = new StartRecording(recorder);
            // button_action_map["stop_recording"] = new StopRecording(recorder);
            foreach (ButtonMapping bm in config_helper.BizDeckConfig.ButtonMap)
            {
                if (!button_action_map.ContainsKey(bm.Name)) {
                    switch (bm.Action) {
                        case "steps":
                            button_action_map[bm.Name] = new StepsButton(config_helper, bm.Name);
                            break;
                        case "app":
                            button_action_map[bm.Name] = new AppButton(config_helper, bm.Name, websock);
                            break;
                        default:
                            logger.Info($"InitButtonActionMap: unknown action[{bm.Action}] for button[{bm.Name}]");
                            break;
                    }
                }
            }
        }
    }
}