using System.Text.Json.Serialization;

namespace BizDeck
{
    /// <summary>
    /// Assign icons to buttons.
    /// </summary>
    public class ButtonMapping
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("button_index")]
        public int ButtonIndex { get; set; }

        [JsonPropertyName("button_image_path")]
        public string ButtonImagePath { get; set; }
    }
}
