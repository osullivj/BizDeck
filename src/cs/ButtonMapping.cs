using System;
using System.Text.Json.Serialization;

namespace BizDeck
{
    public class ButtonMapping
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Indexes are assigned by ConfigHelper code,
        // and not set from JSON
        public int ButtonIndex { get; set; }

        [JsonPropertyName("image")]
        public string ButtonImagePath { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; }
    }
}
