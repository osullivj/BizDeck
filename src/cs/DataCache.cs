using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck {

    public class DataCache {
        // Use of Lazy<T> gives us a thread safe singleton
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<DataCache> lazy =
            new Lazy<DataCache>(() => new DataCache());

        public static DataCache Instance { get { return lazy.Value; } }

        private Dictionary<string, Dictionary<string,List<Dictionary<string, string>>>> cache = new();
        private readonly object cache_lock = new();

        private DataCache() { }

        public void Insert(string key, Dictionary<string,List<Dictionary<string,string>>> val) {
            lock(cache_lock) {
                cache[key] = val;
            }
        }
    }
}
