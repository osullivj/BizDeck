using System;
using System.IO;
using System.Net.WebSockets;
using System.Net.Http;
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
		List<string> http_get_request_keys = new() { "name", "url", "target" };
		List<string> python_script_keys = new() { "name", "path", "args", "env" };
		List<string> app_script_keys = new() { "name"};
		List<string> action_script_keys = new() { "name" };

		public ActionsDriver(ConfigHelper ch, BizDeckWebSockModule ws, BizDeckPython py) {
			logger = new(this);
			app_driver = new(ch, ws);
			config_helper = ch;
			python = py;
			websock = ws;
			dispatchers["http_get"] = this.HTTPGet;
			dispatchers["python"] = this.RunPythonScript;
			dispatchers["app"] = this.RunApp;
			dispatchers["actions"] = this.RunActions;
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
						logger.Info($"PlayActions: action ok index[{action_index}], type[{action_type}]");
                    }
				}
				else {
					logger.Error($"PlayActions: skipping unknown action_type[{action_type}]");
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

		public async Task<(bool, string)> RunPythonScript(JObject action) {
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
				logger.Error($"RunPythonScript: {error}");
				return (false, error);
            }
			return await python.RunScript(python_script_path, options);
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
			var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.HttpGetTimeout));
			string target_path = Path.Combine(new string[] { config_helper.DataDir, target_file_name });
			try {
				var payload = await http_client.GetStringAsync(url, http_cancel_token_source.Token).ConfigureAwait(false);
				// Save the script contents into the dat dir
				await File.WriteAllTextAsync(target_path, payload);
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
	}
}