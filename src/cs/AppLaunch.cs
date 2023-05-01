using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BizDeck
{
    /// <summary>
    /// JSON deserialize object for app launch config.
    /// </summary>
    public class AppLaunch
    {
        [JsonPropertyName("exe_doc_url")]
        public string ExeDocUrl { get; set; }

        [JsonPropertyName("args")]
        public string Args { get; set; }
    }
}
