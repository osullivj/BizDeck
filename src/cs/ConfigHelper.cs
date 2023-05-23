using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

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

        public string DataDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "dat" });
        }

        public string LogDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "logs"});
        }

        public string HtmlDir
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "html" });
        }

        public string IconsDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "icons" });
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
            try {
                TraceConfig = File.ReadAllText(TraceConfigPath);
                string config_json = File.ReadAllText(ConfigPath);
                BizDeckConfig = JsonSerializer.Deserialize<BizDeckConfig>(config_json, json_serializer_options);
                int index = 0;
                foreach (ButtonDefinition bm in BizDeckConfig.ButtonList) {
                    bm.ButtonIndex = index++;
                }
                return BizDeckConfig;
            }
            catch (JsonException ex) {
                // Since we cannot load the config file, we cannot start the web server and
                // we don't know where the location of log dir. So we present the exception
                // in the default browser. But we do have bdroot from the cmd line, so
                // we can save the error there...
                ThrowErrorToBrowser("config.json", ex.ToString());
            }
            return null;
        }

        public void ThrowErrorToBrowser(string context, string error_message) {
            string error_file_path = Path.Combine(new string[] { LocalAppDataPath, "biz_deck_error.html" });
            string html = $"<body><h3>BizDeck {context} error</h3><p>{error_message}</p>";
            File.WriteAllText(error_file_path, html);
            var process = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(error_file_path) {
                    UseShellExecute = true
                }
            };
            process.Start();
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
            BizDeckConfig.ButtonList.RemoveAll(button => button.Name == name);
            logger.Info($"DeleteButton: removed name[{name}]");
            return await SaveConfig();
        }

        public async Task<(bool,string)> AddButton(IconCache icon_cache, string script_name, string script, string background)
        {
            // name will have an extenstion like .json, so remove it...
            string button_name = Path.GetFileNameWithoutExtension(script_name);
            logger.Info($"AddButton: script_name[{script_name}] button_name[{button_name}] bg[{background}]");
            // Does the button already exist?
            int index = BizDeckConfig.ButtonList.FindIndex(button => button.Name == button_name);
            if ( index != -1)
            {
                return (false, $"{ConfigDir}\\{script_name} already exists");
            }
            icon_cache.CreateLabelledIconPNG(background, button_name);
            // Create the new button mapping now so we can populate as we
            // apply checks to the script type.
            ButtonDefinition bm = new();
            bm.Name = button_name;
            bm.ButtonIndex = BizDeckConfig.ButtonList.Count;
            bm.ButtonImagePath = $"icons\\{button_name}.png";
            // Is is an app launch or steps?
            (bool launch_ok, AppLaunch launch, string launch_error) = ValidateAppLaunch(script);
            if (launch_ok) {
                bm.Action = "app";
            }
            else {
                if (script.Contains("steps")) {
                    bm.Action = "steps";
                }
                else if (script.Contains("actions")) {
                    bm.Action = "actions";
                }
                else { 
                    return (false, "Script is not an app launch, or chrome recorder steps, or ETL actions");
                }
            }
            // Save the script contents into the cfg dir
            string script_path = Path.Combine(new string[] { ConfigDir, script_name });
            await File.WriteAllTextAsync(script_path, script);
            // Add the newly created button to config button map
            BizDeckConfig.ButtonList.Add(bm);
            (bool saveOK, string errmsg) = await SaveConfig();
            if (!saveOK)
            {
                return (false, $"{ConfigPath} save failed for new button {script_name}");
            }
            return (true, $"{LogDir}/{script_name} created for button index:{bm.ButtonIndex}, name:{bm.Name}");
        }

        public async Task<(bool, AppLaunch, string)> LoadAppLaunch(string name)
        {
            var app_launch_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", $"{name}.json" });
            var launch_json = await File.ReadAllTextAsync(app_launch_path);
            return ValidateAppLaunch(launch_json);
        }

        public (bool, string) LoadStepsOrActions(string name)
        {
            bool ok = true;
            string result = null;
            var script_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", $"{name}.json" });
            try {
                result = File.ReadAllText(script_path);
            }
            catch (Exception ex) {
                result = $"{name}: failed to read {script_path}, {ex}";
                logger.Error($"LoadStepsOrActions: {result}");
            }
            return (ok, result);
        }

        protected (bool, AppLaunch, string) ValidateAppLaunch(string launch_json)
        {
            var launch = JsonSerializer.Deserialize<AppLaunch>(launch_json);
            if (launch == null)
                return (false, null, "null app_launch");
            if (launch.ExeDocUrl == null)
                return (false, null, "exe_doc_url field not supplied");
            return (true, launch, "");
        }
    }
}
