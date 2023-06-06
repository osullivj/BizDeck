using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BizDeck {

    // TODO: think about theading. It's a remote possibility, but an action could upload
    // to the cache while we're rendering a CacheEntry as HTML

    // One cache row, for the purposes of iterating over any type
    // of cache entry. When we're RegularCSV there's no key, so
    // key will be index.ToString()
    public class CacheEntryRow {
        private Dictionary<string, string> row;
        private string key;

        public Dictionary<string,string> Row { get => row; set => row = value; }
        public string KeyValue { get => key; set => key = value; }

        public CacheEntryRow(Dictionary<string,string> r, string k) {
            row = r;
            key = k;
        }

        public CacheEntryRow() { }
    }

    public enum CacheEntryType {
        PrimaryKeyCSV,
        RegularCSV,
    }

    public class CacheEntry : IEnumerable<CacheEntryRow> {
        [JsonConverter(typeof(StringEnumConverter))]
        public CacheEntryType Type { get; private set; }

        public object CacheValue { get; private set; }

        // CacheValue is an object, so we don't have access to the underlying
        // size without a cast. We could use ICollection as CacheValue type,
        // but that would force us to implement the ICollection interface
        // for any type we want to use that is not an MS ICollection. However,
        // we will have a CacheEntry ctor for each possible underlying type,
        // which means we have type knowledge at ctor time, so we extract a
        // value for count then.
        private int count = 0;
        public int Count {
            get => count;
        }

        // headers gets initialised when a CacheValue is set
        // we provide an empty list as default just in case
        // CacheValue has 0 entries
        private List<string> headers = empty_header_list;
        private static List<string> empty_header_list = new();
        public List<string> Headers {
            get => headers;
        }

        // Keys accessor. NB row_keys and row_key are only set for PrimaryKeyCSV
        private List<string> row_keys = null;
        public List<string> RowKeys { get => row_keys; }
        private string row_key = null;
        public string RowKey { get => row_key; }

        // Upfront casts to simplify the Enumerator impl
        private List<Dictionary<string, string>> as_list = null;
        public List<Dictionary<string, string>> AsList { get => as_list; }
        private Dictionary<string, Dictionary<string, string>> as_dict = null;
        public Dictionary<string, Dictionary<string, string>> AsDict { get => as_dict; }

        // The ctor have type knowledge of Value, so set the helper
        // accessors for Enumerator here. For the List ctor we expect
        // the row_key to be null. For Dict ctor, there should be a key value
        public CacheEntry(List<Dictionary<string, string>> val, string row_key) {
            Type = CacheEntryType.RegularCSV;
            CacheValue = val;
            this.row_key = "";
            count = val.Count;
            as_list = val;
            if (val.Count > 0) {
                headers = val[0].Keys.ToList();
            }
        }

        public CacheEntry(Dictionary<string, Dictionary<string, string>> val, string row_key) {
            Type = CacheEntryType.PrimaryKeyCSV;
            CacheValue = val;
            this.row_key = row_key;
            count = val.Count;
            as_dict = val;
            if (val.Count > 0) {
                headers = val.First().Value.Keys.ToList();
                row_keys = val.Keys.ToList<string>();
            }
        }

        public string GetRowKey(int index) {
            if (index > Count - 1) {
                return null;
            }
            switch (Type) {
                case CacheEntryType.RegularCSV:
                    return index.ToString();
                case CacheEntryType.PrimaryKeyCSV:
                    return RowKeys[index];
                default:
                    throw new NotImplementedException();
            }
        }

        public CacheEntryRow GetRow(int index) {
            if (index > Count - 1 || index < 0) { 
                return null;
            }
            string key;
            switch (Type) {
                case CacheEntryType.RegularCSV:
                    return new CacheEntryRow(AsList[index], index.ToString());
                case CacheEntryType.PrimaryKeyCSV:
                    key = RowKeys[index];
                    return new CacheEntryRow(AsDict[key], key);
                default:
                    throw new NotImplementedException();
            }
        }

        public byte[] GetKeyOrIndexColumnHeader() {
            switch (this.Type) {
                case CacheEntryType.RegularCSV:
                    return HTMLHelpers.IndexColumnName;
                case CacheEntryType.PrimaryKeyCSV:
                    return HTMLHelpers.KeyColumnName;
                default:
                    return HTMLHelpers.EmptyString;
            }
        }

    public IEnumerator<CacheEntryRow> GetEnumerator() {
            return new CacheEntryEnumerator(this);
        }

        private IEnumerator PrivateGetEnumerator() {
            return this.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return this.PrivateGetEnumerator();
        }
    }

    public class CacheEntryEnumerator : IEnumerator<CacheEntryRow> {
        CacheEntry cache_entry;
        int index;
        CacheEntryRow current_row = new();

        public CacheEntryEnumerator(CacheEntry ce) {
            cache_entry = ce;
            index = -1;
        }

        public CacheEntryRow Current => current_row;
        object IEnumerator.Current => current_row;

        public void Dispose() { }

        public bool MoveNext() {
            index++;
            current_row.KeyValue = cache_entry.GetRowKey(index);
            if (current_row.KeyValue == null) {
                // We've hit the end of the rows
                return false;
            }
            switch (cache_entry.Type) {
                case CacheEntryType.RegularCSV:
                    current_row.Row = cache_entry.AsList[index];
                    return true;
                case CacheEntryType.PrimaryKeyCSV:
                    current_row.Row = cache_entry.AsDict[current_row.KeyValue];
                    return true;
                default:
                    throw new NotImplementedException();
            }
        }

        public void Reset() {
            index = -1;
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

        public void Insert(string group, string key, List<Dictionary<string, string>> val) {
            lock (cache_lock) {
                Dictionary<string, CacheEntry> cache_group = null;
                if (cache.ContainsKey(group)) {
                    cache_group = cache[group];
                }
                else {
                    cache_group = new();
                    cache.Add(group, cache_group);
                }
                cache_group[key] = new CacheEntry(val, null);
                changed = true;
            }
        }

        public void Insert(string group, string key, Dictionary<string, Dictionary<string, string>> val, string row_key) {
            lock (cache_lock) {
                Dictionary<string, CacheEntry> cache_group = null;
                if (cache.ContainsKey(group)) {
                    cache_group = cache[group];
                }
                else {
                    cache_group = new();
                    cache.Add(group, cache_group);
                }
                cache_group[key] = new CacheEntry(val, row_key);
                changed = true;
            }
        }

        public string SerializeAndResetChanged() {
            string json = "{}";
            BizDeckJsonEvent cache_update = new("cache");
            lock (cache_lock) {
                cache_update.Data = cache;
                json = JsonConvert.SerializeObject(cache_update);
                changed = false;
            }
            return json;
        }

        public CacheEntry GetCacheEntry(string group, string key) {
            lock (cache_lock) {
                if (cache.ContainsKey(group)) {
                    Dictionary<string, CacheEntry> cache_group = cache[group];
                    if (cache_group.ContainsKey(key)) {
                        return cache_group[key];
                    }
                }
            }
            return null;
        }
    }
}
