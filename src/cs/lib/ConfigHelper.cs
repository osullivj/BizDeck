﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace BizDeck {

    // HttpFormat: used for loading http_formats.json
    public class HttpFormat {
        [JsonPropertyName("format")]
        public string Format { get; set; }

        [JsonPropertyName("values")]
        public List<string> Values { get; set; }
    }

    public class ConfigHelper {
        // Use of Lazy<T> gives us a thread safe singleton
        // Instance property is the access point
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<ConfigHelper> lazy =
            new Lazy<ConfigHelper>(() => new ConfigHelper());
        public static ConfigHelper Instance { get { return lazy.Value; } }

        private CmdLineOptions cmd_line_options;
        private BizDeckLogger logger;
        private JsonSerializerOptions json_serializer_options = new();

        private ConfigHelper() {
            // setup the JSON serialization options used by Load/SaveConfig
            json_serializer_options.AllowTrailingCommas = true;
            json_serializer_options.WriteIndented = true;
        }

        public void Init(CmdLineOptions opts) {
            cmd_line_options = opts;
        }

        #region LocalFSMethods
        public void CreateLogger() {
            // Most classes create their logger in the ctor. Can't do that here
            // as ConfigHelper is instanced before the config is loaded, so we
            // don't know what the log dir is at construction time. This method
            // gets invoked by BizDeckLogger.InitLogging once the log file
            // has been created. JOS 2023-05-08
            logger = new(this);
        }

        public string LocalAppDataPath {
            get => cmd_line_options.appdata;
        }

        public string ConfigPath {
            get => Path.Combine(new string[] { ConfigDir, "config.json" });
        }

        public string TraceConfigPath {
            get => Path.Combine(new string[] { ConfigDir, "trace_config.json" });
        }

        public string SecretsPath {
            get => Path.Combine(new string[] { ConfigDir, "secrets.json" });
        }

        public string HttpFormatsPath {
            get => Path.Combine(new string[] { ConfigDir, "http_formats.json" });
        }

        public string ConfigDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg" });
        }

        public string DataDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "data" });
        }

        public string ScriptsDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "scripts" });
        }

        public string LogDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "logs" });
        }

        public string HtmlDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "html" });
        }

        public string IconsDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "icons" });
        }

        public string PythonCoreSourcePath {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "src", "py", "core" });
        }

        public BizDeckConfig BizDeckConfig { set; get; }

        public string TraceConfig { get; private set; }

        public Dictionary<string, string> Secrets { get; private set; }

        public Dictionary<string, Dictionary<string, HttpFormat>> HttpFormatMap {get; private set;}

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
                string secrets = File.ReadAllText(SecretsPath);
                Secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(secrets, json_serializer_options);
                foreach (var pair in Secrets) {
                    NameStack.Instance.AddNameValue($"secrets.{pair.Key}", pair.Value);
                }
                string http_formats = File.ReadAllText(HttpFormatsPath);
                HttpFormatMap = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, HttpFormat>>>(http_formats, json_serializer_options);
                TraceConfig = File.ReadAllText(TraceConfigPath);
                string config_json = File.ReadAllText(ConfigPath);
                BizDeckConfig = JsonSerializer.Deserialize<BizDeckConfig>(config_json, json_serializer_options);
                int index = 0;
                foreach (ButtonDefinition bm in BizDeckConfig.ButtonList) {
                    bm.ButtonIndex = index++;
                }
                return BizDeckConfig;
            }
            catch (Exception ex) {
                // Since we cannot load the config file, we cannot start the web server and
                // we don't know where the location of log dir. So we present the exception
                // in the default browser. But we do have bdroot from the cmd line, so
                // we can save the error there...
                ThrowErrorToBrowser($"LoadConfig from {ConfigDir}", ex.ToString());
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

        public async Task<(bool,string)> AddButton(string script_name, string script, string background)
        {
            // name will have an extenstion like .json, so remove it...
            string button_name = Path.GetFileNameWithoutExtension(script_name);
            logger.Info($"AddButton: script_name[{script_name}] button_name[{button_name}] bg[{background}]");
            // Does the button already exist?
            int index = BizDeckConfig.ButtonList.FindIndex(button => button.Name == button_name);
            if ( index != -1) {
                return (false, $"{script_name} already exists");
            }
            bool created = IconCache.Instance.CreateLabelledIconPNG(background, button_name);
            if (!created) {
                return (false, $"cannot create {button_name} PNG from background[{background}]");
            }
            // Create the new button mapping now so we can populate as we
            // apply checks to the script type.
            ButtonDefinition bm = new();
            bm.Name = button_name;
            bm.ButtonIndex = BizDeckConfig.ButtonList.Count;
            bm.ButtonImagePath = $"icons\\{button_name}.png";
            // Is is an app launch or steps?
            (bool launch_ok, AppLaunch launch, string launch_error) = ValidateAppLaunch(script);
            if (launch_ok) {
                bm.Action = "apps";
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
            string script_path = Path.Combine(new string[] { ScriptsDir, bm.Action, script_name });
            await File.WriteAllTextAsync(script_path, script);
            // Add the newly created button to config button map
            BizDeckConfig.ButtonList.Add(bm);
            (bool saveOK, string errmsg) = await SaveConfig();
            if (!saveOK) {
                return (false, $"{script_path} save failed for new button {script_name}");
            }
            return (true, $"{script_path}/{script_name} created for button index:{bm.ButtonIndex}, name:{bm.Name}");
        }


        public async Task<(bool, AppLaunch, string)> LoadAppLaunch(string name_or_path)
        {
            // TODO: refactor to use BDAR return type. NB ValidateAppLaunch will have
            // to change as well.
            string app_launch_path = name_or_path;
            if (!File.Exists(name_or_path)) {
                app_launch_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "scripts", "apps", $"{name_or_path}.json" });
            }
            var launch_json = await File.ReadAllTextAsync(app_launch_path);
            return ValidateAppLaunch(launch_json);
        }

        public (bool, string) LoadStepsOrActions(string name_or_path)
        {
            bool ok = true;
            string result = null;
            string script_path = name_or_path;
            if (!File.Exists(name_or_path)) {
                script_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "scripts", "actions", $"{name_or_path}.json" });
                if (!File.Exists(script_path)) {
                    script_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "scripts", "steps", $"{name_or_path}.json" });
                }
            }
            try {
                result = File.ReadAllText(script_path);
            }
            catch (Exception ex) {
                result = $"{name_or_path}: failed to read {script_path}, {ex}";
                logger.Error($"LoadStepsOrActions: {result}");
                ok = false;
            }
            logger.Info($"LoadStepsOrActions: loaded {script_path}");
            return (ok, result);
        }

        // SaveExcelQuery is invoked synchronously by DataCache Insert methods which
        // are in turn invoked bizdeck.py when it adds a cache entry
        public (bool, string) SaveExcelQuery(string group, string cache_key) {
            try {
                string query_path = Path.Combine(ScriptsDir, "excel", $"{group}_{cache_key}.iqy");
                string query_url = $"{BizDeckStatus.Instance.MyURL}/excel/{group}/{cache_key}";
                ExcelQueryHelper query_helper = new(query_url);
                File.WriteAllLines(query_path, query_helper.Lines);
                logger.Info($"SaveExcelQuery: path[{query_path}], url[{query_url}]");
            }
            catch (Exception ex) {
                logger.Error($"SaveExcelQuery: group[{group}], cache_key[{cache_key}]");
                return (false, ex.Message);
            }
            return (true, null);
        }
        #endregion LocalFSMethods

        #region InternalMethods
        protected (bool, AppLaunch, string) ValidateAppLaunch(string launch_json)
        {
            var launch = JsonSerializer.Deserialize<AppLaunch>(launch_json);
            if (launch == null)
                return (false, null, "null app_launch");
            if (launch.ExeDocUrl == null)
                return (false, null, "exe_doc_url field not supplied");
            return (true, launch, "");
        }
        #endregion InternalMethods

    }
}