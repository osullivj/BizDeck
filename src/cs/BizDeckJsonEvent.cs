using System.Collections.Generic;
using Swan.Formatters;

namespace BizDeck
{
    public class BizDeckJsonEvent
    {
        public BizDeckJsonEvent(string type)
        {
            Type = type;
            Data = new Dictionary<string, string>();
        }

        [JsonProperty("type")]
        public string Type { get; private set; }

        [JsonProperty("data")]
        public object Data { get; set; }
    }
}