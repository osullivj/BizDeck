using System;
using System.Linq;
using System.IO;
using System.Net.WebSockets;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {

	// ActionsDrive executes ETL style actions
	// eg pull data from REST APIs
	public class ActionsDriver	{
		ConfigHelper config_helper;
		BizDeckLogger logger;
		HttpClient http_client = new();
		BizDeckWebSockModule websock;
		AppDriver app_driver;
		Dictionary<string, Dispatch> dispatchers = new();
		Dictionary<string, HttpMethod> http_method_map = new();
		List<string> http_get_request_keys = new() { "name", "url", "target" };
		List<string> python_script_keys = new() { "name", "path", "args", "env" };
		List<string> python_action_non_param_keys = new() { "type", "function"};
		List<string> app_script_keys = new() { "name"};
		List<string> action_script_keys = new() { "name" };
		BizDeckResult success = new();

		// We allow a null websock ctor param so that BizDeckApiController
		// can construct for API actions invocation. In that scenario we
		// don't update GUI cache tab during API invocation. We leave
		public ActionsDriver(BizDeckWebSockModule ws = null) {
			logger = new(this);
			app_driver = new(ws);
			config_helper = ConfigHelper.Instance;
			websock = ws;
			dispatchers["http_get"] = this.HTTPGet;
			dispatchers["python_batch"] = this.RunPythonBatchScript;
			dispatchers["python_action"] = this.RunPythonAction;
			dispatchers["app"] = this.RunApp;
			dispatchers["actions"] = this.RunActions;

			http_method_map["http_get"] = HttpMethod.Get;
			http_method_map["http_post"] = HttpMethod.Post;
		}

		// actions should be a JObject corresponding to the contents of
		// eg quandl_rates.json
		public async Task<BizDeckResult> PlayActions(string name, dynamic actions)
        {
			logger.Info($"PlayActions: playing {name}");
			JArray action_array = actions.actions;
			int action_index = 0;
			bool fail_ok = false;
			BizDeckResult result = null;
			foreach (JObject action in action_array) {
				string action_type = (string)action.GetValue("type");
				fail_ok = false;
				if (action.ContainsKey("fail_ok")) {
					fail_ok = (bool)action.GetValue("fail_ok");
                }
				if (dispatchers.ContainsKey(action_type)) {
					result = await dispatchers[action_type](action);
					if (!result.OK) {
						logger.Error($"PlayActions: action failed index[{action_index}], type[{action_type}], err[{result}]");
						if (!fail_ok) {
							return result;
						}
                    }
					else {
						logger.Info($"PlayActions: name[{name}] action ok index[{action_index}], type[{action_type}]");
						// The action succeeded. It may have updated the DataCache, so check if it's
						// changed, and if so, send to the GUI.
						if (DataCache.Instance.HasChanged && websock != null) {
							string cache_state_json = DataCache.Instance.SerializeToJsonEvent(true);
							await websock.BroadcastJson(cache_state_json);
                        }
                    }
				}
				else {
					logger.Error($"PlayActions: name[{name}] skipping unknown index[{action_index}] action_type[{action_type}]");
                }
				action_index++;
            }
			return success;
		}

		public async Task<BizDeckResult> RunApp(JObject action) {
			string app_script_name = null;
			string error = null;
			if (app_script_keys.TrueForAll(s => action.ContainsKey(s))) {
				app_script_name = (string)action["name"];
			}
			else {
				error = $"one of {app_script_keys} missing from {action}";
				logger.Error($"RunApp: {error}");
				return new BizDeckResult(error);
			}
			return await app_driver.PlayApp(app_script_name);
		}

		public async Task<BizDeckResult> RunActions(JObject action) {
			string action_script_name = null;
			string error = null;
			if (action_script_keys.TrueForAll(s => action.ContainsKey(s))) {
				action_script_name = (string)action["name"];
			}
			else {
				error = $"one of {action_script_keys} missing from {action}";
				logger.Error($"RunActions: {error}");
				return new BizDeckResult(error);
			}
			// Yes, we're recursing here!
			JObject action_script = LoadAndParseActionScript(action_script_name);
			if (action_script == null) {
				error = $"LoadAndParseActionScript failed:{action_script_name}";
				logger.Error($"RunActions: {error}");
				return new BizDeckResult(error);
			}
			return await PlayActions(action_script_name, action_script);
		}

		public async Task<BizDeckResult> RunPythonBatchScript(JObject action) {
			string python_script_path = null;
			string action_name = null;
			JObject env = null;
			JArray args = null;
			string error = null;
			Dictionary<string, object> options = null;
			if (python_script_keys.TrueForAll(s => action.ContainsKey(s))) {
				action_name = (string)action["name"];
				python_script_path = (string)action["path"];
				args = action["args"] as JArray;
				env = action["env"] as JObject;
				if (args != null && args.Count > 0) {
					var arg_list = new List<string>(args.Count);
					// args is an array of strings, not objects,
					// so we use JToken as the JObject autocast
					// throws here...
					foreach(JToken arg in args) {
						arg_list.Add((string)arg);
                    }
					options = new();
					options["Arguments"] = arg_list;
                }
			}
			else {
				error = $"one of {python_script_keys} missing from {action}";
				logger.Error($"RunPythonBatchScript: {error}");
				return new BizDeckResult(error);
            }
			return await BizDeckPython.Instance.RunBatchScript(python_script_path, options);
        }

		public async Task<BizDeckResult> RunPythonAction(JObject action) {
			string python_action_function = (string)action["function"];
			// we check the members of action with TrueForAll in other
			// methods. But we don't know here what parameters are expected
			// by the action function, so we compose a list for marshalling
			// in BizDeckPython, excluding type and function, which together
			// select the function to call. The others are params. We use
			// dynamic so we can have lists and dicts as params as well as
			// atomic types.
			Dictionary<string, dynamic> args = new();
			args["cache"] = DataCache.Instance;
			foreach (KeyValuePair<string, JToken> param in action) {
				if (!python_action_non_param_keys.Contains(param.Key)) {
					// param.Value is a JToken. We don't want type leakage
					// over to the Python side, so use ToString to force
					// the param to be a .Net built in type.
					args.Add(param.Key, param.Value.ToString());
				}
            }
			return await BizDeckPython.Instance.RunActionFunction(python_action_function, args);
		}

		public async Task<BizDeckResult> HTTPGet(JObject action) {
			string url = null;
			string target_file_name = null;
			string action_name = null;
			string error = null;
			BizDeckResult result = null;
			if (http_get_request_keys.TrueForAll(s => action.ContainsKey(s))) {
				action_name = (string)action["name"];
				url = (string)action["url"];
				target_file_name = (string)action["target"];
			}
			else {
				error = $"one of {http_get_request_keys} missing from {action}";
				logger.Error($"HTTPGet: {error}");
				return new BizDeckResult(error);
			}
			string data_sub_dir = Path.GetExtension(target_file_name).TrimStart('.');
			string target_dir = Path.Combine(new string[] { config_helper.DataDir, data_sub_dir});
			string target_path = Path.Combine(new string[] { target_dir, target_file_name});
			try {
				// this may be a file extension we haven't encountered, so ensure the
				// %BDROOT%/dat/<data_sub_dir> folder exists
				System.IO.Directory.CreateDirectory(target_dir);
				result = BuildHttpRequest(url, "http_get", action);
				if (!result.OK) {
					return result;
                }
				HttpRequestMessage hrm = (HttpRequestMessage)result.Payload;
				logger.Info($"HTTPGet: hrm.url[{hrm.RequestUri}]");
				var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.HttpGetTimeout));
				var payload = await http_client.SendAsync(hrm, http_cancel_token_source.Token);
				if (payload.StatusCode != System.Net.HttpStatusCode.OK) {
					error = $"status[{payload.StatusCode}] for url[{hrm.RequestUri}]";
					logger.Error($"HTTPGet: status[{payload.StatusCode}] for url[{hrm.RequestUri}]");
					return new BizDeckResult(error);
                }
				// Save the script contents into the dat dir
				Stream file_write_stream = File.OpenWrite(target_path);
				await payload.Content.CopyToAsync(file_write_stream);
				logger.Info($"HTTPGet: {action_name} saved to {target_path}");
				return success;
			}
			catch (Exception ex) {
				error = $"{action_name} failed to save to {target_path}, {ex}";
				logger.Error($"HTTPGet: {error}");
				return new BizDeckResult(error);
			}
		}

		public JObject LoadAndParseActionScript(string name_or_path) {
			string result = null;
			BizDeckResult load_result = config_helper.LoadStepsOrActions(name_or_path);
			if (!load_result.OK) {
				return null;
			}
			try {
				return JObject.Parse(load_result.Message);
			}
			catch (JsonReaderException ex) {
				result = $"JSON error reading {name_or_path}, {ex}";
				logger.Error($"LoadAndParseActionScript: {result}");
			}
			return null;
		}

		private BizDeckResult ExpandHttpFormat(HttpFormat hf, JObject action) {
			BizDeckResult resolve_result = null;
			List<string> resolved_values = new();
			using (var scope = NameStack.Instance.LocalScope(action)) {
				foreach (string val_ref in hf.Values) {
					resolve_result = scope.Resolve(val_ref);
					if (!resolve_result.OK) {
						logger.Error($"Resolve({val_ref}) failed in action[{action}]");
						return resolve_result;
                    }
					resolved_values.Add(resolve_result.Message);
				}
			}
			try {
				return new BizDeckResult(true, String.Format(hf.Format, resolved_values.ToArray()));
			}
			catch (Exception ex) {
				// catch formatting exceptions
				logger.Error($"ExpandHttpFormat: failed in action[{ action.ToString()}]");
				logger.Error($"ExpandHttpFormat: refs{hf.Values.ToString()}");
				logger.Error($"ExpandHttpFormat: vals{resolved_values.ToString()}");
				return new BizDeckResult(ex.Message);
            }
        }

		private BizDeckResult BuildHttpRequest(string url, string method, JObject action) {
			HttpRequestMessage request = null;
			string expanded_url = url;
			bool ok = false;
			HttpFormat bd_http_format = null;
			Dictionary<string, HttpFormat> http_spec_map = null;
			BizDeckResult result = null;
			// Does the HttpFormatMap have a key that occurs in our unexpanded URL?
			foreach (string url_sub_string in config_helper.HttpFormatMap.Keys) {
				if (url.Contains(url_sub_string)) {
					// We have formats for this target URL. For example, to
					// add "?auth_token=<token>" to quandl HTTP requests
					http_spec_map = config_helper.HttpFormatMap[url_sub_string];
					// Does the Dictionary<string,HttpFormat> have a url expansion definition?
					if (http_spec_map.ContainsKey("url")) {
						// We have a url expansion, which we must do before we
						// fire the HttpRequestMessage ctor
						bd_http_format = http_spec_map["url"];
						result = ExpandHttpFormat(bd_http_format, action);
						if (!result.OK) {
							return result;
						}
						else {
							expanded_url = result.Message;
                        }
					}
				}
            }
			// We need a url and method to construct HttpRequestMessage,
			// and we should have both in hand now...
			HttpMethod http_method = HttpMethod.Get;
			if (http_method_map.ContainsKey(method)) {
				http_method = http_method_map[method];
            }
			request = new HttpRequestMessage(http_method, expanded_url);
			// We have a request in hand. If we found a BizDeck.HttpFormat
			// specify any headers?
			if (http_spec_map != null) {
				foreach (string key in http_spec_map.Keys) {
					if (key != "url") {
						// If it's not a url, then we assume a header
						// TODO: when we implement POST, we likely can't
						// assume other spec fields are headers.
						bd_http_format = http_spec_map[key];
						result = ExpandHttpFormat(bd_http_format, action);
						if (ok) {
							request.Headers.Add(key, (IEnumerable<string>)result.Payload);
                        }
						else {
							// expanded_header will be an err msg
							return result;
                        }
					}
                }
            }
			return new BizDeckResult(true, request);
        }
	}
}