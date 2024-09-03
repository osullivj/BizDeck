using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
        private Regex name_stack_ref = new(@"<(.*?)>");

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

        public BizDeckResult Interpolate(string field) {
            MatchCollection matches = name_stack_ref.Matches(field);

            // No matches, so no need for variable substitution
            if (matches.Count == 0) {
                return new BizDeckResult(true, field);
            }
            // One or more matches...
            StringBuilder sb = new();
            int field_index = 0;
            foreach (Match match in matches) {
                if (match.Success && match.Groups.Count > 0) {
                    // match.Groups == ['<cargo_id>', 'cargo_id']
                    var var_name = match.Groups[1].Value;
                    var var_result = NameStack.Instance.Resolve(var_name);
                    if (!var_result.OK) {
                        return var_result;
                    }
                    // We have a match, and a replacement. Was there a preamble to the 
                    // current match we need to copy into the interpolated result?
                    if (match.Index > field_index) {
                        sb.Append(field.Substring(field_index, match.Index));
                    }
                    sb.Append(var_result.Payload);
                    field_index = match.Index + match.Groups[0].Length;
                }
            }
            // We've handled the matches, and any prefix text before each match. Is there any
            // suffix text?
            if (field_index < field.Length - 1) {
                sb.Append(field.Substring(field_index));
            }
            return new BizDeckResult(true, sb.ToString());
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
