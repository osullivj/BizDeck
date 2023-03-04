using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BizDeck {
    public class ConfigHelper {
        public string LocalAppDataPath {
            get => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public string ConfigPath {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "config", "config.json"});
        }

        public string LayoutPath
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "config", "layout.json" });
        }

        public string LayoutRulesPath
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "config", "layout_rules.json" });
        }

        public string LogDir {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "logs"});
        }

        public string HtmlDir
        {
            get => Path.Combine(new string[] { LocalAppDataPath, "BizDeck", "html" });
        }

        public BizDeckConfig LoadConfig() {
            return JsonSerializer.Deserialize<BizDeckConfig>(File.ReadAllText(ConfigPath));
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
