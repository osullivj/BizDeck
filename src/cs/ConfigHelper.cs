using System;
using System.IO;
using System.Text.Json;

namespace BizDeck {
    public class ConfigHelper {
        private CmdLineOptions options;
        public ConfigHelper(CmdLineOptions opts) { options = opts; }
        public string LocalAppDataPath {
            get => options.bdroot;
        }

        public string ConfigPath {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", "config.json"});
        }

        public string TraceConfigPath
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", "trace_config.json" });
        }

        public string LogDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "logs"});
        }

        public string HtmlDir
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "html" });
        }

        public BizDeckConfig BizDeckConfig { set; get; }

        public string TraceConfig { get; private set; }

        public string GetFullIconPath(string button_image_path)
        {
            return Path.Combine(new string[] { LocalAppDataPath, "BizDeck", button_image_path });
        }

        public string GetFullLogPath()
        {
            return Path.Combine(new string[] { LocalAppDataPath, "BizDeck", BizDeckConfig.BrowserUserDataDir });
        }

        public BizDeckConfig LoadConfig() {
            try
            {
                TraceConfig = File.ReadAllText(TraceConfigPath);
                BizDeckConfig = JsonSerializer.Deserialize<BizDeckConfig>(File.ReadAllText(ConfigPath));
                int index = 0;
                foreach (ButtonMapping bm in BizDeckConfig.ButtonMap)
                {
                    bm.ButtonIndex = index++;
                }
                return BizDeckConfig;
            }
            catch (JsonException ex)
            {
                // Since we cannot load the config file, we cannot start the web server and
                // we don't know where the location of log dir. So stdout is the best we can do...
                System.Console.Write($"{ex.ToString()}");   
            }
            return null;
        }

        public AppLaunch LoadAppLaunch(string name)
        {
            var app_launch_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", $"{name}.json" });
            var launch = JsonSerializer.Deserialize<AppLaunch>(File.ReadAllText(app_launch_path));
            return launch;
        }

        public string LoadSteps(string name)
        {
            var steps_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", $"{name}.json" });
            var steps = File.ReadAllText(steps_path);
            return steps;
        }
    }
}
