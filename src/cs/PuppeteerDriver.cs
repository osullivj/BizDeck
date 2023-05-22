using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PuppeteerSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
	public delegate Task<(bool, string)> Dispatch(JObject step);

	public class PuppeteerDriver
	{
		LaunchOptions launch_options = new();
		ConfigHelper config_helper;
		BizDeckLogger logger;
		Dictionary<string, Dispatch> dispatchers = new();
		IBrowser browser;
		IPage current_page;
		JObject pending_viewport_step;
		int selector_index;

		public PuppeteerDriver(ConfigHelper ch)
		{
			logger = new(this);
			config_helper = ch;
			selector_index = ch.BizDeckConfig.SelectorIndex;

			dispatchers["setViewport"] = this.SetViewport;
			dispatchers["navigate"] = this.Navigate;
			dispatchers["click"] = this.Click;
			dispatchers["change"] = this.Change;

			// setup browser process launch options
			launch_options.Headless = config_helper.BizDeckConfig.Headless;
			launch_options.ExecutablePath = config_helper.BizDeckConfig.BrowserPath;
			launch_options.UserDataDir = config_helper.GetFullLogPath();
			launch_options.Devtools = config_helper.BizDeckConfig.DevTools;
			launch_options.Args = new string[1] { $"--remote-debugging-port={config_helper.BizDeckConfig.BrowserRecorderPort}" };
		}

		public async Task<(bool, string)> PlaySteps(string name, dynamic chrome_recording)
        {
			logger.Info($"PlaySteps: playing {name} on browser[{launch_options.ExecutablePath}]");
			logger.Info($"PlaySteps: UserDataDir[{launch_options.UserDataDir}], Headless[{launch_options.Headless}], Devtools[{launch_options.Devtools}]");
			// Clear state left over from previous PlaySteps
			current_page = null;
			pending_viewport_step = null;
			// create browser instance
			browser = await Puppeteer.LaunchAsync(launch_options);
			logger.Info($"PlaySteps: playing[{chrome_recording.title}]");
			JArray steps = chrome_recording.steps;
			bool step_ok = false;
			string error = null;
			int step_index = 0;
			foreach (JObject step in steps) {
				string step_type = (string)step.GetValue("type");
				if (dispatchers.ContainsKey(step_type)) {
					(step_ok, error) = await dispatchers[step_type](step);
					if (!step_ok) {
						error = $"step failed index[{step_index}], type[{step_type}], err[{error}]";
						logger.Error($"PlaySteps: {error}");
						return (false, error);
                    }
					else {
						logger.Info($"PlaySteps: step ok index[{step_index}], type[{step_type}]");
                    }
				}
				else {
					logger.Error($"PlaySteps: skipping unknown step_type[{step_type}]");
                }
				step_index++;
            }
			return (true, null);
		}

		public async Task<(bool, string)> SetViewport(JObject step)
        {
			if (current_page == null) {
				pending_viewport_step = step;
			}
			else {
				ViewPortOptions vpo = new();
				vpo.Height = (int)step["height"];
				vpo.Width = (int)step["width"];
				vpo.DeviceScaleFactor = (int)step["deviceScaleFactor"];
				vpo.IsMobile = (bool)step["isMobile"];
				vpo.HasTouch = (bool)step["hasTouch"];
				vpo.IsLandscape = (bool)step["isLandscape"];
				await current_page.SetViewportAsync(vpo);
            }
			return (true, null);
        }

		public async Task<(bool, string)> Navigate(JObject step)
        {
			bool ok = false;
			string error = null;
			string url = (string)step["url"];
			if (url == "chrome://new-tab-page/") {
				current_page = await browser.NewPageAsync();
				if (pending_viewport_step != null) {
					(ok, error) = await SetViewport(pending_viewport_step);
					pending_viewport_step = null;
					return (ok, error);
                }
			}
			else {
				IResponse http_response = await current_page.GoToAsync(url);
				if (http_response.Status != System.Net.HttpStatusCode.OK) {
					error = $"bad status[{http_response.StatusText}] for url[{url}]";
					logger.Error($"Navigate: {error}");
					return (false, error);
                }
            }
			return (true, null);
        }

		public async Task<(bool, string)> Click(JObject step)
        {
			JArray selectors = (JArray)step["selectors"];
			string selector = (string)(selectors[selector_index][0]);
			await current_page.ClickAsync(selector);
			return (true, null);
        }

		public async Task<(bool, string)> Change(JObject step)
		{
			JArray selectors = (JArray)step["selectors"];
			string selector = (string)(selectors[selector_index][0]);
			string new_value = (string)step["value"];
			string extra_value = "";
			if (step.ContainsKey("extra_value"))
				extra_value = (string)step["extra_value"];
			var element_handle = await current_page.QuerySelectorAsync(selector);
			await element_handle.TypeAsync(new_value+extra_value);
			return (true, null);
		}
	}
}