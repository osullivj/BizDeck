using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BizDeck
{
    /// <summary>
    /// Configuration profile that represents the mapping of buttons on a Stream Deck device
    /// to icons, as well as HTTP port, USD device index, and true/false for a DOS box console.
    /// </summary>
    public class BizDeckSteps
    {
        [JsonPropertyName("exe_doc_url")]
        public string ExeDocUrl { get; set; }

        [JsonPropertyName("args")]
        public string Args { get; set; }
    }
}
