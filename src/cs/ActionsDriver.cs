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
		Dictionary<string, Dispatch> dispatchers = new();
		int selector_index;
		List<string> http_get_request_keys = new() { "name", "url", "target" };

		public ActionsDriver(ConfigHelper ch)
		{
			logger = new(this);
			config_helper = ch;
			selector_index = ch.BizDeckConfig.SelectorIndex;

			dispatchers["http_get"] = this.HTTPGet;
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
			foreach (JObject action in action_array) {
				string action_type = (string)action.GetValue("type");
				if (dispatchers.ContainsKey(action_type)) {
					(action_ok, error) = await dispatchers[action_type](action);
					if (!action_ok) {
						logger.Error($"PlayActions: action failed index[{action_index}], type[{action_type}], err[{error}]");
						return (false, error);
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

		public async Task<(bool, string)> HTTPGet(JObject action)
        {
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

	}
}