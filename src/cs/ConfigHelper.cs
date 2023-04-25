using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CommandLine;

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
            TraceConfig = File.ReadAllText(TraceConfigPath);
            BizDeckConfig = JsonSerializer.Deserialize<BizDeckConfig>(File.ReadAllText(ConfigPath));
            return BizDeckConfig;
        }

        public BizDeckSteps LoadSteps(string name)
        {
            var steps_path = Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", $"{name}.json" });
            var steps = JsonSerializer.Deserialize<BizDeckSteps>(File.ReadAllText(steps_path));
            return steps;
        }
    }
}
