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
            get => Path.Combine(new string[] { ConfigDir, "config.json"});
        }

        public string TraceConfigPath
        {
            get => Path.Combine(new string[] { ConfigDir, "trace_config.json" });
        }

        public string ConfigDir
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg"});
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

        public async Task<(bool,string)> SaveConfig()
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
                string error_msg = $"SaveConfig: JSON serialization fail {ex}";
                logger.Error(error_msg);
                return (false, error_msg);
            }
            return (true, ConfigPath);
        }

        public async Task<(bool,string)> DeleteButton(string name)
        {
            BizDeckConfig.ButtonMap.RemoveAll(button => button.Name == name);
            logger.Info($"DeleteButton: removed name[{name}]");
            return await SaveConfig();
        }

        public async Task<(bool,string)> AddButton(string script_name, string script)
        {
            // name will have an extenstion like .json, so remove it...
            string button_name = Path.GetFileNameWithoutExtension(script_name);
            logger.Info($"AddButton: script_name[{script_name}] button_name[{button_name}]");
            // Does the button already exist?
            int index = BizDeckConfig.ButtonMap.FindIndex(button => button.Name == button_name);
            if ( index != -1)
            {
                return (false, $"{ConfigDir}\\{script_name} already exists");
            }
            // Create the new button mapping now so we can populate as we
            // apply checks to the script type.
            ButtonMapping bm = new();
            bm.Name = button_name;
            bm.ButtonIndex = BizDeckConfig.ButtonMap.Count;
            bm.ButtonImagePath = "icons\\record2.png";
            // Is is an app launch or steps?
            var launch = JsonSerializer.Deserialize<AppLaunch>(script);
            if (launch == null)
            {
                if (!script.Contains("steps"))
                {
                    return (false, "Script is not an app launch or recorder steps");
                }
                bm.Action = "steps";
            }
            else
            {
                bm.Action = "app";
            }
            // Save the script contents into the cfg dir
            string script_path = Path.Combine(new string[] { ConfigDir, script_name });
            await File.WriteAllTextAsync(ConfigPath, script);
            (bool saveOK, string errmsg) = await SaveConfig();
            if (!saveOK)
            {
                return (false, $"{ConfigPath} save failed for new button {script_name}");
            }
            return (true, $"{LogDir}/{script_name} created for button index:{bm.ButtonIndex}, name:{bm.Name}");
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
