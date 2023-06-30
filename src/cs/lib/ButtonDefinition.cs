using System;
using System.Text.Json.Serialization;

namespace BizDeck {

    public class ButtonDefinition {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Indexes are assigned by ConfigHelper code,
        // and not set from JSON
        [JsonIgnore]
        public int ButtonIndex { get; set; }

        [JsonPropertyName("image")]
        public string ButtonImagePath { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonIgnore]
        public bool Set { get; set; }

        [JsonPropertyName("blink")]
        public bool Blink { get; set; }
    }
}
