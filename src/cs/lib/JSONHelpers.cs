using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {

    public class CacheEntryConverter : JsonConverter {

        public override bool CanConvert(Type objectType) {
            // We're not interested in building CacheEntry instance from JSON
            return objectType == typeof(CacheEntry);
        }

        public override bool CanWrite {
            get { return true; }
        }

        public override bool CanRead {
            get { return false; }
        }

        public override object ReadJson(JsonReader reader, Type t, object value, JsonSerializer serializer) {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            CacheEntry cache_entry = (CacheEntry)value;
            JObject root_jobj = new();
            // First, the meta data that describes the CacheEntry
            root_jobj.Add("type", new JValue(cache_entry.Type.ToString()));
            root_jobj.Add("count", new JValue(cache_entry.Count));
            root_jobj.Add("row_key", new JValue(cache_entry.RowKey));
            root_jobj.Add("headers", new JArray(cache_entry.Headers));
            // Now for the data
            JArray data_array = new JArray();
            foreach (CacheEntryRow row in cache_entry) {
                data_array.Add(JObject.FromObject(row.Row));
            }
            root_jobj.Add("data", data_array);
            root_jobj.WriteTo(writer);
        }
    }
}
