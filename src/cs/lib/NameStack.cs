using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace BizDeck {

	// NameStack resolves references 
	public class NameStack {
        // Use of Lazy<T> gives us a thread safe singleton
        // Instance property is the access point
        // https://csharpindepth.com/articles/singleton
        private static readonly Lazy<NameStack> lazy =
            new Lazy<NameStack>(() => new NameStack());
        public static NameStack Instance { get { return lazy.Value; } }

        private Dictionary<string, string> global = new();
        private BizDeckResult fail = new BizDeckResult(false, "unresolved"); 

        private NameStack() { }

        // No locking as contents of global will not change
        // after ConfigHelper.LoadConfig
        public void AddNameValue(string key, string val) {
            global[key] = val;
        }

        public BizDeckResult Resolve(string key) {
            if (global.ContainsKey(key)) {
                return new BizDeckResult(true, global[key]);
            }
            return fail;
        }

        public class Scope : IDisposable {
            JObject local = null;
            public Scope(JObject local) { this.local = local; }
            public void Dispose() { }   // null op: no resources to release

            public BizDeckResult Resolve(string key) {
                if (local.ContainsKey(key)) {
                    try {
                        return new BizDeckResult(true, (string)local[key]);
                    }
                    catch (Exception ex) {
                        return new BizDeckResult(false, ex.Message);
                    }
                }
                return NameStack.Instance.Resolve(key);
            }
        }

        public Scope LocalScope(JObject local) {
            return new Scope(local);
        }
	}
}
