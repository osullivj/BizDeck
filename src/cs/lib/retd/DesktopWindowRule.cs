using System;
using System.Text.Json.Serialization;
using System.Windows.Automation;

namespace BizDeck
{
    // JSON serialization class for reading and writing layout_rules.json.
    public class DesktopWindowRule
    {
        public DesktopWindowRule()
        {
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("class_name")]
        public string ClassName { get; set; }

        [JsonPropertyName("exe")]
        public string Exe { get; set; }
    }
}

