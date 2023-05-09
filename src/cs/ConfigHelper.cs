using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace BizDeck {
    public class ConfigHelper {
        private CmdLineOptions cmd_line_options;
        private BizDeckLogger logger;
        private JsonSerializerOptions json_serializer_options = new();

        public ConfigHelper(CmdLineOptions opts) {
            cmd_line_options = opts;

            // setup the JSON serialization options used by Load/SaveConfig
            json_serializer_options.AllowTrailingCommas = true;
            json_serializer_options.WriteIndented = true;
        }

        public void CreateLogger()
        {
            // Most classes create their logger in the ctor. Can't do that here
            // as ConfigHelper is instanced before the config is loaded, so we
            // don't know what the log dir is at construction time. This method
            // gets invoked by BizDeckLogger.InitLogging once the log file
            // has been created. JOS 2023-05-08
            logger = new(this);
        }

        public string LocalAppDataPath {
            get => cmd_line_options.bdroot;
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
                BizDeckConfig = JsonSerializer.Deserialize<BizDeckConfig>(File.ReadAllText(ConfigPath), 
                                                                                json_serializer_options);
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

        public async Task<bool> SaveConfig()
        {
            try
            {
                string config_json = JsonSerializer.Serialize<BizDeckConfig>(BizDeckConfig, 
                                                                    json_serializer_options);
                await File.WriteAllTextAsync(ConfigPath, config_json);
                logger.Info($"SaveConfig: config saved to path[{ConfigPath}]");
            }
            catch (JsonException ex)
            {
                // Since we cannot load the config file, we cannot start the web server and
                // we don't know where the location of log dir. So stdout is the best we can do...
                logger.Error($"SaveConfig: JSON serialization fail {ex}");
                return false;
            }
            return true;
        }

        public async Task<bool> DeleteButton(string name)
        {
            BizDeckConfig.ButtonMap.RemoveAll(button => button.Name == name);
            logger.Info($"DeleteButton: removed name[{name}]");
            bool ok = await SaveConfig();
            return ok;
        }

        public async Task<bool> AddButton(dynamic button_data)
        {
            // BizDeckConfig.ButtonMap.RemoveAll(button => button.Name == name);
            logger.Info($"AddButton: recved button_data[{button_data}]");
            bool ok = await SaveConfig();
            return ok;
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
