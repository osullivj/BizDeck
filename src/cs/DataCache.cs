using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {

    public class DataCache {
        // Use of Lazy<T> gives us a thread safe singleton
        // Instance property is the access point
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<DataCache> lazy =
            new Lazy<DataCache>(() => new DataCache());
        public static DataCache Instance { get { return lazy.Value; } }

        private Dictionary<string, Dictionary<string,List<Dictionary<string, string>>>> cache = new();
        private readonly object cache_lock = new();
        private bool changed = false;

        public bool HasChanged { get => changed; }

        private DataCache() { }

        public void Insert(string key, Dictionary<string,List<Dictionary<string,string>>> val) {
            lock(cache_lock) {
                cache[key] = val;
                changed = true;
            }
        }

        public string SerializeAndResetChanged() {
            string json = "{}";
            BizDeckJsonEvent cache_update = new("cache");
            lock(cache_lock) {
                cache_update.Data = cache;
                json = JsonConvert.SerializeObject(cache_update);
                changed = false;
            }
            return json;
        }
    }
}
