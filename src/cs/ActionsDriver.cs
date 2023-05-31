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
		BizDeckPython python;
		BizDeckWebSockModule websock;
		AppDriver app_driver;
		Dictionary<string, Dispatch> dispatchers = new();
		Dictionary<string, HttpMethod> http_method_map = new();
		List<string> http_get_request_keys = new() { "name", "url", "target" };
		List<string> python_script_keys = new() { "name", "path", "args", "env" };
		List<string> python_action_non_param_keys = new() { "type", "function"};
		List<string> app_script_keys = new() { "name"};
		List<string> action_script_keys = new() { "name" };


		public ActionsDriver(ConfigHelper ch, BizDeckWebSockModule ws, BizDeckPython py) {
			logger = new(this);
			app_driver = new(ch, ws);
			config_helper = ch;
			python = py;
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
		public async Task<(bool, string)> PlayActions(string name, dynamic actions)
        {
			logger.Info($"PlayActions: playing {name}");
			JArray action_array = actions.actions;
			bool action_ok = false;
			string error = null;
			int action_index = 0;
			bool fail_ok = false;
			foreach (JObject action in action_array) {
				string action_type = (string)action.GetValue("type");
				fail_ok = false;
				if (action.ContainsKey("fail_ok")) {
					fail_ok = (bool)action.GetValue("fail_ok");
                }
				if (dispatchers.ContainsKey(action_type)) {
					(action_ok, error) = await dispatchers[action_type](action);
					if (!action_ok) {
						logger.Error($"PlayActions: action failed index[{action_index}], type[{action_type}], err[{error}]");
						if (!fail_ok) {
							return (false, error);
						}
                    }
					else {
						logger.Info($"PlayActions: name[{name}] action ok index[{action_index}], type[{action_type}]");
						// The action succeeded. It may have updated the DataCache, so check if it's
						// changed, and if so, send to the GUI.
						if (DataCache.Instance.HasChanged) {
							string cache_state_json = DataCache.Instance.SerializeAndResetChanged();
							await websock.BroadcastJson(cache_state_json);
                        }
                    }
				}
				else {
					logger.Error($"PlayActions: name[{name}] skipping unknown index[{action_index}] action_type[{action_type}]");
                }
				action_index++;
            }
			return (true, null);
		}

		public async Task<(bool, string)> RunApp(JObject action) {
			string app_script_name = null;
			string error = null;
			if (app_script_keys.TrueForAll(s => action.ContainsKey(s))) {
				app_script_name = (string)action["name"];
			}
			else {
				error = $"one of {app_script_keys} missing from {action}";
				logger.Error($"RunApp: {error}");
				return (false, error);
			}
			return await app_driver.PlayApp(app_script_name);
		}

		public async Task<(bool, string)> RunActions(JObject action) {
			string action_script_name = null;
			string error = null;
			if (action_script_keys.TrueForAll(s => action.ContainsKey(s))) {
				action_script_name = (string)action["name"];
			}
			else {
				error = $"one of {action_script_keys} missing from {action}";
				logger.Error($"RunActions: {error}");
				return (false, error);
			}
			// Yes, we're recursing here!
			JObject action_script = LoadAndParseActionScript(action_script_name);
			if (action_script == null) {
				error = $"LoadAndParseActionScript failed:{action_script_name}";
				logger.Error($"RunActions: {error}");
				return (false, error);
			}
			return await PlayActions(action_script_name, action_script);
		}

		public async Task<(bool, string)> RunPythonBatchScript(JObject action) {
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
				return (false, error);
            }
			return await python.RunBatchScript(python_script_path, options);
        }

		public async Task<(bool, string)> RunPythonAction(JObject action) {
			string python_action_function = (string)action["function"];
			// we check the members of action with TrueForAll in other
			// methods. But we don't know here what parameters are expected
			// by the action function, so we compose a list for marshalling
			// in BizDeckPython, excluding type and function, which together
			// select the function to call. The others are params. We use
			// dynamic so we can have lists and dicts as params as well as
			// atomic types.
			List<dynamic> args = new() { DataCache.Instance };
			foreach (KeyValuePair<string, JToken> param in action) {
				if (!python_action_non_param_keys.Contains(param.Key)) {
					// param.Value is a JToken. We don't want type leakage
					// over to the Python side, so use ToString to force
					// the param to be a .Net built in type.
					args.Add(param.Value.ToString());
				}
            }
			return await python.RunActionFunction(python_action_function, args);
		}

		public async Task<(bool, string)> HTTPGet(JObject action) {
			string url = null;
			string target_file_name = null;
			string action_name = null;
			string error = null;
			if (http_get_request_keys.TrueForAll(s => action.ContainsKey(s))) {
				action_name = (string)action["name"];
				url = (string)action["url"];
				target_file_name = (string)action["target"];
			}
			else {
				error = $"one of {http_get_request_keys} missing from {action}";
				logger.Error($"HTTPGet: {error}");
				return (false, error);
			}		
			string target_path = Path.Combine(new string[] { config_helper.DataDir, target_file_name });
			try {
				(bool ok, object request_or_err) = BuildHttpRequest(url, "http_get", action);
				if (!ok) {
					return (ok, (string)request_or_err);
                }
				HttpRequestMessage hrm = (HttpRequestMessage)request_or_err;
				logger.Info($"HTTPGet: hrm.url[{hrm.RequestUri}]");
				var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.HttpGetTimeout));
				var payload = await http_client.SendAsync(hrm, http_cancel_token_source.Token);
				if (payload.StatusCode != System.Net.HttpStatusCode.OK) {
					error = $"status[{payload.StatusCode}] for url[{hrm.RequestUri}]";
					logger.Error($"HTTPGet: status[{payload.StatusCode}] for url[{hrm.RequestUri}]");
					return (false, error);
                }
				// Save the script contents into the dat dir
				Stream file_write_stream = File.OpenWrite(target_path);
				await payload.Content.CopyToAsync(file_write_stream);
				logger.Info($"HTTPGet: {action_name} saved to {target_path}");
				return (true, null);
			}
			catch (Exception ex) {
				error = $"{action_name} failed to save to {target_path}, {ex}";
				logger.Error($"HTTPGet: {error}");
				return (false, error);
			}
		}

		public JObject LoadAndParseActionScript(string name_or_path) {
			bool ok = true;
			string result = null;
			(ok, result) = config_helper.LoadStepsOrActions(name_or_path);
			if (!ok) {
				return null;
			}
			try {
				return JObject.Parse(result);
			}
			catch (JsonReaderException ex) {
				result = $"JSON error reading {name_or_path}, {ex}";
				logger.Error($"LoadAndParseActionScript: {result}");
			}
			return null;
		}

		private (bool, string) ExpandHttpFormat(HttpFormat hf, JObject action) {
			List<string> resolved_values = new();
			bool ok = false;
			string resolved_value = null;
			using (var scope = NameStack.Instance.LocalScope(action)) {
				foreach (string val_ref in hf.Values) {
					(ok, resolved_value) = scope.Resolve(val_ref);
					if (!ok) {
						return (false, $"Resolve({val_ref}) failed in action[{action.ToString()}]");
                    }
					resolved_values.Add(resolved_value);
				}
			}
			try {
				return (true, String.Format(hf.Format, resolved_values.ToArray()));
			}
			catch (Exception ex) {
				// catch formatting exceptions
				logger.Error($"ExpandHttpFormat: failed in action[{ action.ToString()}]");
				logger.Error($"ExpandHttpFormat: refs{hf.Values.ToString()}");
				logger.Error($"ExpandHttpFormat: vals{resolved_values.ToString()}");
				return (false, ex.Message);
            }
        }

		private (bool, object) BuildHttpRequest(string url, string method, JObject action) {
			HttpRequestMessage request = null;
			string expanded_url = url;
			bool ok = false;
			HttpFormat bd_http_format = null;
			Dictionary<string, HttpFormat> http_spec_map = null;
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
						(ok, expanded_url) = ExpandHttpFormat(bd_http_format, action);
						if (!ok) {
							// expanded_url is an err msg
							return (false, expanded_url);
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
			string expanded_header = null;
			// We have a request in hand. If we found a BizDeck.HttpFormat
			// specify any headers?
			if (http_spec_map != null) {
				foreach (string key in http_spec_map.Keys) {
					if (key != "url") {
						// If it's not a url, then we assume a header
						// TODO: when we implement POST, we likely can't
						// assume other spec fields are headers.
						bd_http_format = http_spec_map[key];
						(ok, expanded_header) = ExpandHttpFormat(bd_http_format, action);
						if (ok) {
							request.Headers.Add(key, expanded_header);
                        }
						else {
							// expanded_header will be an err msg
							return (ok, expanded_header);
                        }
					}
                }
            }
			return (true, request);
        }
	}
}