using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {

    // HttpFormat: used for loading http_formats.json
    public class HttpFormat {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("values")]
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
        private JsonSerializerSettings json_serializer_settings = new();

        private ConfigHelper() {
            json_serializer_settings.Formatting = Formatting.Indented;
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

        public string BDRoot {
            get => cmd_line_options.bdroot;
        }

        public string ConfigPath {
            get => cmd_line_options.config_path;
        }

        public string HttpFormatsPath {
            get => Path.Combine(new string[] { ConfigDir, "http_formats.json" });
        }

        public string ConfigDir {
            get => cmd_line_options.config_dir;
        }

        public string DataDir {
            get => Path.Combine(new string[] { BDRoot, "data" });
        }

        public string ScriptsDir {
            get => Path.Combine(new string[] { BDRoot, "scripts" });
        }

        public string LogDir {
            get => Path.Combine(new string[] { BDRoot, "logs" });
        }

        public string HtmlDir {
            get => Path.Combine(new string[] { BDRoot, "html" });
        }

        public string IconsDir {
            get => Path.Combine(new string[] { BDRoot, "icons" });
        }

        // We attempt to load secrets from BizDeckConfig.SecretsPath
        // If that fails we attempt to load from ConfigDir/BizDeckConfig.SecretsPath
        public string ActualSecretsPath { private set; get; }

        public string PythonCoreSourcePath {
            get => Path.Combine(new string[] { BDRoot, "src", "py", "core" });
        }

        public BizDeckConfig BizDeckConfig { set; get; }

        public string TraceConfig { get; private set; }

        public Dictionary<string, string> Secrets { get; private set; }

        public Dictionary<string, Dictionary<string, HttpFormat>> HttpFormatMap {get; private set;}

        public string GetFullIconPath(string button_image_path)
        {
            return Path.Combine(new string[] { BDRoot, button_image_path });
        }

        public string GetFullLogPath()
        {
            return Path.Combine(new string[] { BDRoot, BizDeckConfig.BrowserUserDataDir });
        }

        public  BizDeckResult LoadConfig() {
            // remember we cannot use the logger here as we the log dir comes from config
            try {
                if (!File.Exists(ConfigPath)) {
                    return BizDeckResult.BadConfigPath;
                }
                // load cfg/http_formats.json
                string http_formats = File.ReadAllText(HttpFormatsPath);

                HttpFormatMap = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, HttpFormat>>>(http_formats);
                // load cfg/config.json
                string config_json = File.ReadAllText(ConfigPath);
                BizDeckConfig = JsonConvert.DeserializeObject<BizDeckConfig>(config_json);
                // loads secrets NV json dict
                string secrets = null;
                string[] fallback_path_elements = { ConfigDir, BizDeckConfig.SecretsPath };
                string secrets_fallback_path = Path.Combine(fallback_path_elements);
                if (File.Exists(BizDeckConfig.SecretsPath)) {
                    ActualSecretsPath = BizDeckConfig.SecretsPath;
                    secrets = File.ReadAllText(BizDeckConfig.SecretsPath);
                }
                else if (File.Exists(secrets_fallback_path)) {
                    ActualSecretsPath = secrets_fallback_path;
                    secrets = File.ReadAllText(secrets_fallback_path);
                }
                else {
                    ActualSecretsPath = "no_secrets_loaded";
                }
                if (secrets != null) {
                    Secrets = JsonConvert.DeserializeObject<Dictionary<string, string>>(secrets);
                }
                else {
                    Secrets = new Dictionary<string, string>();
                }
                foreach (var pair in Secrets) {
                    NameStack.Instance.AddNameValue($"secrets.{pair.Key}", pair.Value);
                }
                // set ButtonDefinition members not supplied in config.json
                // init button state is set: set with main image. Set is false
                // when the button has been blanked.
                int index = 0;
                foreach (ButtonDefinition bd in BizDeckConfig.ButtonList) {
                    bd.ButtonIndex = index++;
                    bd.Set = true;
                }
                return BizDeckResult.Success;
            }
            catch (Exception ex) {
                // Since we cannot load the config file, we cannot start the web server and
                // we don't know where the location of log dir. So we present the exception
                // in the default browser. But we do have bdroot from the cmd line, so
                // we can save the error there...
                ThrowErrorToBrowser($"LoadConfig from {ConfigDir}", ex.ToString());
            }
            return BizDeckResult.BadConfigPath;
        }

        public void ThrowErrorToBrowser(string context, string error_message) {
            string error_file_path = Path.Combine(new string[] { LogDir, "biz_deck_error.html" });
            string html = $"<body><h3>BizDeck {context} error</h3><p>{error_message}</p>";
            File.WriteAllText(error_file_path, html);
            var process = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(error_file_path) {
                    UseShellExecute = true
                }
            };
            process.Start();
        }


        public async Task<BizDeckResult> SaveConfig() {
            try {
                string config_json = JsonConvert.SerializeObject(BizDeckConfig, json_serializer_settings);
                await File.WriteAllTextAsync(ConfigPath, config_json);
                logger.Info($"SaveConfig: config saved to path[{ConfigPath}]");
            }
            catch (JsonException ex) {
                // Since we cannot load the config file, we cannot start the web server and
                // we don't know where the location of log dir. So stdout is the best we can do...
                string error_msg = $"SaveConfig: JSON serialization fail {ex}";
                logger.Error(error_msg);
                return new BizDeckResult(error_msg);
            }
            return new BizDeckResult(true, ConfigPath);
        }

        public async Task<BizDeckResult> DeleteButton(string name)
        {
            BizDeckConfig.ButtonList.RemoveAll(button => button.Name == name);
            logger.Info($"DeleteButton: removed name[{name}]");
            return await SaveConfig();
        }

        public async Task<BizDeckResult> AddButton(string script_name, string script, string background, 
                                                    bool blink=false, ButtonMode mode=ButtonMode.Persistent)
        {
            // name will have an extenstion like .json, so remove it...
            string button_name = Path.GetFileNameWithoutExtension(script_name);
            logger.Info($"AddButton: script_name[{script_name}] button_name[{button_name}] bg[{background}]");
            // Does the button already exist?
            int index = BizDeckConfig.ButtonList.FindIndex(button => button.Name == button_name);
            if ( index != -1) {
                return new BizDeckResult($"{script_name} already in ButtonList");
            }
            bool created = IconCache.Instance.CreateLabelledIconPNG(background, button_name);
            if (!created) {
                return new BizDeckResult($"cannot create {button_name} PNG from background[{background}]");
            }
            // Create the new button mapping now so we can populate as we
            // apply checks to the script type.
            ButtonDefinition bd = new();
            bd.Name = button_name;
            bd.ButtonIndex = BizDeckConfig.ButtonList.Count;
            bd.ButtonImagePath = $"icons\\{button_name}.png";
            bd.Blink = blink;
            bd.Mode = mode;
            // Is is an app launch or steps?
            BizDeckResult validation = ValidateAppLaunch(script);
            if (validation.OK) {
                bd.Action = ButtonImplType.Apps;
            }
            else {
                if (script.Contains("steps")) {
                    bd.Action = ButtonImplType.Steps;
                }
                else if (script.Contains("actions")) {
                    bd.Action = ButtonImplType.Actions;
                }
                else { 
                    return new BizDeckResult("Script is not an app launch, or chrome recorder steps, or ETL actions");
                }
            }
            // Save the script contents into the scripts dir
            string script_path = Path.Combine(new string[] { ScriptsDir, bd.ImplTypeAsString, script_name });
            await File.WriteAllTextAsync(script_path, script);
            // Add the newly created button to config button map
            BizDeckConfig.ButtonList.Add(bd);
            BizDeckResult save_result = await SaveConfig();
            if (!save_result.OK) {
                return new BizDeckResult($"{script_path} save failed for new button {script_name}");
            }
            return new BizDeckResult(true, $"{script_path}/{script_name} created for {bd}");
        }


        public async Task<BizDeckResult> LoadAppLaunch(string name_or_path)
        {
            // TODO: refactor to use BDAR return type. NB ValidateAppLaunch will have
            // to change as well.
            string app_launch_path = name_or_path;
            if (!File.Exists(name_or_path)) {
                app_launch_path = Path.Combine(new string[] { BDRoot, "scripts", "apps", $"{name_or_path}.json" });
            }
            var launch_json = await File.ReadAllTextAsync(app_launch_path);
            return ValidateAppLaunch(launch_json);
        }

        public BizDeckResult LoadStepsOrActions(string name_or_path)
        {
            bool ok = true;
            string result = null;
            string script_path = name_or_path;
            if (!File.Exists(name_or_path)) {
                script_path = Path.Combine(new string[] { BDRoot, "scripts", "actions", $"{name_or_path}.json" });
                if (!File.Exists(script_path)) {
                    script_path = Path.Combine(new string[] { BDRoot, "scripts", "steps", $"{name_or_path}.json" });
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
            return new BizDeckResult(ok, result);
        }

        // SaveExcelQuery is invoked synchronously by DataCache Insert methods which
        // are in turn invoked bizdeck.py when it adds a cache entry
        public BizDeckResult SaveExcelQuery(string group, string cache_key) {
            try {
                string query_path = Path.Combine(ScriptsDir, "excel", $"{group}_{cache_key}.iqy");
                string query_url = $"{BizDeckStatus.Instance.MyURL}/excel/{group}/{cache_key}";
                ExcelQueryHelper query_helper = new(query_url);
                File.WriteAllLines(query_path, query_helper.Lines);
                logger.Info($"SaveExcelQuery: path[{query_path}], url[{query_url}]");
            }
            catch (Exception ex) {
                logger.Error($"SaveExcelQuery: group[{group}], cache_key[{cache_key}]");
                return new BizDeckResult(ex.Message);
            }
            return BizDeckResult.Success;
        }
        #endregion LocalFSMethods

        #region InternalMethods
        protected BizDeckResult ValidateAppLaunch(string launch_json)
        {
            var launch = JsonConvert.DeserializeObject<AppLaunch>(launch_json);
            if (launch == null)
                return new BizDeckResult("null app_launch");
            if (launch.ExeDocUrl == null)
                return new BizDeckResult("exe_doc_url field not supplied");
            return new BizDeckResult(true, launch);
        }
        #endregion InternalMethods

    }
}
