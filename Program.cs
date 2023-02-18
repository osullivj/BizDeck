namespace BizDeck
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using EmbedIO;
    using EmbedIO.WebApi;
    using EmbedIO.Files;
    using EmbedIO.Actions;
    using Swan.Logging;

    class Program
    {
        private const bool UseFileCache = true;
        private static Dictionary<string, ButtonAction> button_action_map = new Dictionary<string, ButtonAction>();
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">The arguments.</param>
        static void Main(string[] args)
        {
            // first load the config
            var config_helper = new ConfigHelper();
            var config = config_helper.LoadConfig();
            var url = string.Format("http://localhost:{0}/", config.HTTPServerPort);
            if (args.Length > 0)
                url = args[0];

            if (config.Console)
            {
                Win32.AllocConsole();
            }
            InitLogging(config_helper.LogDir);
            InitButtonActionMap(url, new Layout(config_helper));

            // Our web server is disposable.
            using (var server = CreateWebServer(url, config_helper.HtmlDir))  {
                // Once we've registered our modules and configured them, we
                // call the RunAsync() method.
                server.RunAsync();

                var device = DeviceManager.SetupDevice(config);
                var exitSignal = new ManualResetEvent(false);
                device.OnButtonPress += (s, e) => {
                    $"Button {e.Id} pressed. Event type: {e.Kind}".Info();
                    if (e.Kind == ButtonEventKind.DOWN) {
                        var buttonEntry = config.ButtonMap.FirstOrDefault(x => x.ButtonIndex == e.Id);
                        if (buttonEntry != null) {
                           ExecuteButtonAction(buttonEntry, device);
                        }
                    }
                };
                device.InitializeDevice();
                // Wait for any key to be pressed before disposing of our web server.
                // In a service, we'd manage the lifecycle of our web server using
                // something like a BackgroundWorker or a ManualResetEvent.
                if (config.Console)
                {
                    Console.ReadKey(true);
                }
            }
        }

        private static void ExecuteButtonAction(ButtonMapping button, ConnectedDevice device, int activatingButton = -1)
        {
            if (button_action_map.ContainsKey(button.Name)) {
                button_action_map[button.Name].Run();
                return;
            }
            $"No action mapped for button {button.Name}".Info();
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string url, string html_path)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                // First, we will configure our web server by adding Modules.
                .WithLocalSessionManager()
                .WithWebApi("/api", m => m
                    .WithController<PeopleController>())
                .WithStaticFolder("/", html_path, true, m => m
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

        private static void InitButtonActionMap(string biz_deck_gui_url, Layout layout)
        {
            button_action_map["gui"] = new ShowBizDeckGUI(biz_deck_gui_url);
            button_action_map["snap_layout"] = new SnapLayout(layout);
        }
    }
}