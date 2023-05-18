using System;
using System.Text.Json.Serialization;

namespace BizDeck
{

    public class BizDeckStatus
    {
        [JsonPropertyName("deck_connection")]
        public bool DeckConnection { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        // Yes, this is a copy of a BizDeckConfig setting
        // We have it here too for ease of adding to GUI
        // data cache to enable GUI defaults to be set
        // from the backend, not from hardcoding in
        // .hx or .xml Haxe impl
        [JsonPropertyName("background_default")]
        public string BackgroundDefault { get; set; }
    }
}
