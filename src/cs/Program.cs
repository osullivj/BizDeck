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
    using Swan.Logging;

    class Program
    {
        private const bool UseFileCache = true;
        private static Dictionary<string, ButtonAction> button_action_map = new Dictionary<string, ButtonAction>();
        private static ConnectedDevice stream_deck = null;
        private static ConfigHelper config_helper = null;
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

            var recorder = new DevToolsRecorder(config_helper);
            InitLogging(config_helper.LogDir);

            // Our web server is disposable.
            using (var server = CreateWebServer(url, config_helper))  {
                // Once we've registered our modules and configured them, we
                // call the RunAsync() method.
                var http_server_task = server.RunAsync();
                http_server_task.ConfigureAwait(false);
                var exitSignal = new ManualResetEvent(false);
                // Now connect to StreamDeck, and start async read
                stream_deck = DeviceManager.SetupDevice(config_helper);
                if (stream_deck == null)
                {
                    // TODO: this error condition should pop up a browser
                    // instance with an explanatory error message, and two
                    // options: exit or retry
                    $"StreamDeck init failed - is it plugged in?".Error();
                }
                else
                {
                    InitButtonActionMap(url, recorder);
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
        private static WebServer CreateWebServer(string url, ConfigHelper ch)
        {
            var websock = new BizDeckWebSockModule(ch);
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
            server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            return server;
        }

        private static void InitLogging(string log_dir) {
            // Swan's FileLogger takes care of inserting a date
            // suffix in the log path as 2nd paran true means "daily"
            var log_path = Path.Combine(new string[] { log_dir, "biz_deck.log" });
            var logger = new FileLogger(log_path, true);
            Logger.RegisterLogger(logger);
        }

        private static void InitButtonActionMap(string biz_deck_gui_url, IRecorder recorder)
        {
            button_action_map["page"] = new Pager(stream_deck);
            button_action_map["gui"] = new ShowBizDeckGUI(biz_deck_gui_url);
            button_action_map["start_recording"] = new StartRecording(recorder);
            button_action_map["stop_recording"] = new StopRecording(recorder);
            foreach (ButtonMapping bm in config_helper.BizDeckConfig.ButtonMap)
            {
                if (!button_action_map.ContainsKey(bm.Name)) {
                    button_action_map[bm.Name] = new StepsButton(config_helper, bm.Name);
                }
            }
        }
    }
}