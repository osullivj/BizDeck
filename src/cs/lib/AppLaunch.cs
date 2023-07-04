using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace BizDeck {
    /// <summary>
    /// JSON deserialize object for app launch config.
    /// </summary>
    public class AppLaunch {
        [JsonProperty("exe_doc_url")]
        public string ExeDocUrl { get; set; }

        [JsonProperty("args")]
        public string Args { get; set; }
    }
}
