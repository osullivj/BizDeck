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

        public string LayoutPath
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", "layout.json" });
        }

        public string LayoutRulesPath
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "cfg", "layout_rules.json" });
        }

        public string LogDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "logs"});
        }

        public string HtmlDir
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "html" });
        }

        public BizDeckConfig BizDeckConfig { set; get; }

        public string GetFullIconPath(string button_image_path)
        {
            return Path.Combine(new string[] { LocalAppDataPath, "BizDeck", button_image_path });
        }

        public string GetFullLogPath()
        {
            return Path.Combine(new string[] { LocalAppDataPath, "BizDeck", BizDeckConfig.EdgeUserDataDir });
        }

        public BizDeckConfig LoadConfig() {
            BizDeckConfig = JsonSerializer.Deserialize<BizDeckConfig>(File.ReadAllText(ConfigPath));
            return BizDeckConfig;
        }

        public BizDeckLayoutRules LoadLayoutRules()
        {
            return JsonSerializer.Deserialize<BizDeckLayoutRules>(File.ReadAllText(LayoutRulesPath));
        }

        public void SaveLayout(List<DesktopWindow> desktop_window_list)
        {
            var options = new JsonSerializerOptions() { WriteIndented = true };
            var new_layout = new BizDeckLayout(desktop_window_list);
            var json_string = JsonSerializer.Serialize<BizDeckLayout>(new_layout, options);
            File.WriteAllText(LayoutPath, json_string);
        }

        public BizDeckLayout LoadLayout()
        {
            return JsonSerializer.Deserialize<BizDeckLayout>(File.ReadAllText(LayoutPath));
        }
    }
}
