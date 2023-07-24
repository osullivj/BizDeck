using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    public class BizDeckJsonEvent {

        public BizDeckJsonEvent(string type) {
            Type = type;
            Data = new Dictionary<string, string>();
        }

        [JsonProperty("type")]
        public string Type { get; private set; }

        [JsonProperty("data")]
        public object Data { get; set; }
    }

    public class BizDeckResult {
        [JsonProperty("ok")]
        public bool OK { get => ok; set => ok = value; }
        private bool ok = true;

        [JsonIgnore]    // Internal use only: on the wire it's always BizDeckResult.Message
        public object Payload { get => payload; set => payload = value;}
        private object payload;
        
        [JsonProperty("message")]
        public string Message { get => payload as string; set => payload = value; }

        // Convenienc statics to reduce the number of BizDeckResult instances
        // constructed at run time
        public static BizDeckResult Success { get => success; }
        private static BizDeckResult success = new(true, null);
        public static BizDeckResult NullSelectorArray { get => null_selector_array; }
        private static BizDeckResult null_selector_array = new(false, "SelectorsToList: null selectors array");
        public static BizDeckResult EmptySelectorArray { get => empty_selector_array; }
        private static BizDeckResult empty_selector_array = new(false, "SelectorsToList: empty selectors array");
        public static BizDeckResult NoCurrentPage { get => no_current_page; }
        private static BizDeckResult no_current_page = new(false, "QuerySelectorAsync: no current page");
        public static BizDeckResult NoSelectorResolves { get => no_selector_resolves; }
        private static BizDeckResult no_selector_resolves = new(false, "QuerySelectorAsync: no selector resolves");
        public static BizDeckResult StreamDeckNotConnected { get => stream_deck_not_connected; }
        private static BizDeckResult stream_deck_not_connected = new(false, "StreamDeck not connected");
        public static BizDeckResult BadConfigPath{ get => bad_config_path; }
        private static BizDeckResult bad_config_path = new(false, "Bad config path, check --appdata param");
        public static BizDeckResult BadAddButtonPayload { get => bad_add_button_payload; }
        private static BizDeckResult bad_add_button_payload = new(false, "Bad /api/add_button payload, check logs");
        public static BizDeckResult BadActionsScript { get => bad_actions_script; }
        private static BizDeckResult bad_actions_script = new(false, "Bad actions script, check logs");

        // Use this ctor to construct both success and fail results
        public BizDeckResult(bool ok, object val) {
            this.ok = ok;
            this.payload = val;
        }

        // Error ctor sets ok false and val to err msg
        public BizDeckResult(string error) {
            ok = false;
            Message = error;
        }

        // Success ctor with null payload
        public BizDeckResult() {
            ok = true;
            payload = null;
        }

        // ctor to work with code returning tuples
        public BizDeckResult( (bool,string) tup) {
            ok = tup.Item1;
            payload = tup.Item2;
        }

        public BizDeckResult( (bool,object) tup) {
            ok = tup.Item1;
            payload = tup.Item2;
        }

        // Convenience for string interpolators
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}