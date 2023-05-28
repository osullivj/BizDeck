using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BizDeck {

    public enum CacheEntryType {
        PrimaryKeyCSV,
        RegularCSV,
    }

    public class CacheEntry {
        [JsonConverter(typeof(StringEnumConverter))]
        public CacheEntryType Type { get; private set; }

        public object CacheValue { get; private set; }

        public CacheEntry(List<Dictionary<string, string>> val) {
            Type = CacheEntryType.RegularCSV;
            CacheValue = val;
        }

        public CacheEntry(Dictionary<string, Dictionary<string, string>> val) {
            Type = CacheEntryType.PrimaryKeyCSV;
            CacheValue = val;
        }
    }

    public class DataCache {
        // Use of Lazy<T> gives us a thread safe singleton
        // Instance property is the access point
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<DataCache> lazy =
            new Lazy<DataCache>(() => new DataCache());
        public static DataCache Instance { get { return lazy.Value; } }

        private Dictionary<string, Dictionary<string, CacheEntry>> cache = new();
        private readonly object cache_lock = new();
        private bool changed = false;

        public bool HasChanged { get => changed; }

        private DataCache() { }

        public void Insert(string group, string key, List<Dictionary<string,string>> val) {
            lock(cache_lock) {
                Dictionary<string, CacheEntry> cache_group = null;
                if (cache.ContainsKey(group)) {
                    cache_group = cache[group];
                }
                else {
                    cache_group = new();
                    cache.Add(group, cache_group);
                }
                cache_group[key] = new CacheEntry(val);
                changed = true;
            }
        }

        public void Insert(string group, string key, Dictionary<string, Dictionary<string, string>> val) {
            lock (cache_lock) {
                Dictionary<string, CacheEntry> cache_group = null;
                if (cache.ContainsKey(group)) {
                    cache_group = cache[group];
                }
                else {
                    cache_group = new();
                    cache.Add(group, cache_group);
                }
                cache_group[key] = new CacheEntry(val);
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
