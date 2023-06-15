using System;
using System.Text.Json.Serialization;

namespace BizDeck {

    public class BizDeckStatus {

        // Use of Lazy<T> gives us a thread safe singleton
        // Instance property is the access point
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<BizDeckStatus> lazy =
            new Lazy<BizDeckStatus>(() => new BizDeckStatus());
        public static BizDeckStatus Instance { get { return lazy.Value; } }

        private BizDeckStatus() { }

        [JsonPropertyName("deck_connection")]
        public bool DeckConnection { get; set; }

        [JsonPropertyName("start_time")]
        public string StartTime { get; set; }

        // Yes, this is a copy of a BizDeckConfig setting
        // We have it here too for ease of adding to GUI
        // data cache to enable GUI defaults to be set
        // from the backend, not from hardcoding in
        // .hx or .xml Haxe impl
        public string BackgroundDefault { get; set; }

        public string DeviceName { get; set; }
        public int ButtonCount { get; set; }
        public int ButtonSize { get; set; }
        public int Brightness { get; set; }

        public string MyURL { get; set; }
    }
}
