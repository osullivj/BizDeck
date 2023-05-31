using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using PuppeteerSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Note the ref to PuppeteerSharp, which implements the DevTools wire 
// protocol, and throws exceptions. Since we're doing multithreaded
// async here, we cannot have unhandled exceptions as they cause
// deadlocks. Consequently, every invocation of a P# method here
// is wrapped in try/catch blocks.

namespace BizDeck {

	public delegate Task<(bool, string)> Dispatch(JObject step);
		// define this driver's own callback dispatch signature
		// for handling Chrome DevTools Recorder steps

	public class PuppeteerDriver {
		LaunchOptions launch_options = new();
		ConfigHelper config_helper;
		BizDeckLogger logger;
		Dictionary<string, Dispatch> dispatchers = new();
		IBrowser browser;
		IPage current_page;
		JObject pending_viewport_step;
		WaitForSelectorOptions wait_for_selector_options = new();

		public PuppeteerDriver(ConfigHelper ch)
		{
			logger = new(this);
			config_helper = ch;

			dispatchers["setViewport"] = this.SetViewport;
			dispatchers["navigate"] = this.Navigate;
			dispatchers["click"] = this.Click;
			dispatchers["change"] = this.Change;
			dispatchers["keyDown"] = this.KeyDown;
			dispatchers["keyUp"] = this.KeyUp;

			// setup browser process launch options
			launch_options.Headless = config_helper.BizDeckConfig.Headless;
			launch_options.ExecutablePath = config_helper.BizDeckConfig.BrowserPath;
			launch_options.UserDataDir = config_helper.GetFullLogPath();
			launch_options.Devtools = config_helper.BizDeckConfig.DevTools;
			launch_options.Args = new string[1] { $"--remote-debugging-port={config_helper.BizDeckConfig.BrowserRecorderPort}" };

			// WaitForSelectorOptions
			wait_for_selector_options.Hidden = false;
			wait_for_selector_options.Timeout = config_helper.BizDeckConfig.HttpGetTimeout;
			wait_for_selector_options.Visible = true;
		}

		public async Task<(bool, string)> PlaySteps(string name, dynamic chrome_recording)
        {
			logger.Info($"PlaySteps: playing {name} on browser[{launch_options.ExecutablePath}]");
			logger.Info($"PlaySteps: UserDataDir[{launch_options.UserDataDir}], Headless[{launch_options.Headless}], Devtools[{launch_options.Devtools}]");
			// Clear state left over from previous PlaySteps
			current_page = null;
			pending_viewport_step = null;
			// Create browser instance. NB we're not doing ConfigureAwait(false)
			// to enable resumption on another thread, but that is what happens
			// in the logs. So there must be a ConfigureAwait() in PuppeteerSharp.
			try {
				browser = await Puppeteer.LaunchAsync(launch_options);
			}
			catch (Exception ex) {
				logger.Error($"PlaySteps: browser launch failed: {ex}");
				return (false, ex.Message);
            }
			JArray steps = chrome_recording.steps;
			logger.Info($"PlaySteps: playing[{chrome_recording.title}], {steps.Count} steps");
			bool step_ok = false;
			string error = null;
			int step_index = 0;
			foreach (JObject step in steps) {
				string step_type = (string)step.GetValue("type");
				logger.Info($"PlaySteps: playing step {step_index}, type[{step_type}]");
				if (dispatchers.ContainsKey(step_type)) {
					(step_ok, error) = await dispatchers[step_type](step);
					if (!step_ok) {
						error = $"step {step_index}, type[{step_type}], err[{error}], ok[{step_ok}]";
						logger.Error($"PlaySteps: played {error}");
						return (false, error);
                    }
					else {
						logger.Info($"PlaySteps: played step {step_index}, type[{step_type}], ok[{step_ok}]");
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
				logger.Info($"SetViewport: ViewPortOptions[{JsonConvert.SerializeObject(vpo)}]");
				try {
					await current_page.SetViewportAsync(vpo);
				}
				catch (Exception ex) {
					logger.Error($"SetViewport: failed {ex}");
					return (false, ex.Message);
				}
			}
			return (true, null);
        }

		public async Task<(bool, string)> Navigate(JObject step)
        {
			bool ok = false;
			string error = null;
			IResponse http_response = null;
			string url = (string)step["url"];
			if (current_page == null) {
				logger.Info($"Navigate: NewPageAsync for url[{url}]");
				// This will resume on another thread, despite no
				// ConfigureAwait(): P# must fo it internally.
				try {
					current_page = await browser.NewPageAsync();
				}
				catch (Exception ex) {
					logger.Error($"Navigate: NewPageAsync failed {ex}");
					return (false, ex.Message);
				}
				if (pending_viewport_step != null) {
					(ok, error) = await SetViewport(pending_viewport_step);
					pending_viewport_step = null;
					if (!ok) {
						return (ok, error);
					}
                }
			}
			// NB the HTML should be fully rendered when GoToAsync returns
			// But it may be possible that JS will causes elements to
			// render aftrwards...
			logger.Error($"Navigate: GoToAsync({url})");
			try {
				http_response = await current_page.GoToAsync(url);
			}
			catch (Exception ex) {
				logger.Error($"Navigate: NewPageAsync url[{url}] {ex}");
				return (false, ex.Message);
			}
			if (http_response.Status != System.Net.HttpStatusCode.OK) {
				error = $"bad status[{http_response.StatusText}] for url[{url}]";
				logger.Error($"Navigate: {error}");
				return (false, error);
            }
			return (true, null);
        }

		public async Task<(bool, string)> Click(JObject step)
        {
			JArray selectors = (JArray)step["selectors"];
			try { 
				(bool query_ok, object element_or_error) = await QuerySelectorListAsync(selectors);
				if (!query_ok) {
					return (query_ok, (string)element_or_error);
                }
				IElementHandle element_handle = (IElementHandle)element_or_error;
				await element_handle.ClickAsync();
			}
			catch (Exception ex) {
				logger.Error($"Click: {ex}");
				return (false, ex.Message);
            }
			return (true, null);
        }

		public async Task<(bool, string)> Change(JObject step)
		{
			JArray selectors = (JArray)step["selectors"];
			(bool sel_ok, object sel_or_err) = await QuerySelectorListAsync(selectors);
			if (!sel_ok) {
				return (sel_ok, (string)sel_or_err);
            }
			string new_value = (string)step["value"];
			string extra_value = "";
			if (step.ContainsKey("extra_value")) {
				extra_value = (string)step["extra_value"];
			}
			try {
				IElementHandle element_handle = (IElementHandle)sel_or_err;
				await element_handle.TypeAsync(new_value + extra_value);
			}
			catch (Exception ex) {
				logger.Error($"Change: {ex}");
				return (false, ex.Message);
            }
			return (true, null);
		}

		public async Task<(bool, string)> KeyDown(JObject step) {
			string key = null;
			try {
				key = (string)step["key"];
				await current_page.Keyboard.DownAsync(key);
			}
			catch (Exception ex) {
				logger.Error($"KeyDown: key[{key}] {ex}");
				return (false, ex.Message);
			}
			return (true, null);
		}

		public async Task<(bool, string)> KeyUp(JObject step) {
			string key = null;
			try {
				key = (string)step["key"];
				await current_page.Keyboard.UpAsync(key);
			}
			catch (Exception ex) {
				logger.Error($"KeyUp: key[{key}] {ex}");
				return (false, ex.Message);
			}
			return (true, null);
		}

		private async Task<(bool, object)> QuerySelectorListAsync(JArray selectors) {
			if (current_page == null) {
				return (false, "QuerySelectorAsync: no current page");
            }
			(bool ok, object err_or_element) = SelectorsToList(selectors);
			if (!ok) {
				return (ok, err_or_element);
            }
			List<string> selector_list = (List<string>)err_or_element;
			IElementHandle element = null;
			foreach (var selector in selector_list) {
				try {
					// See CustomQueriesMAnager.GetQueryHandlerAndSelector() in P#
					// The method potentially throws an exception, if we don't 
					// handle here it causes deadlock
					element = await current_page.QuerySelectorAsync(selector);
					if (element != null) {
						return (true, element);
                    }
				}
				catch (Exception ex) {
					logger.Error($"QuerySelectorAsync: selector[{selector}], {ex}");
                }
			}
			return (false, JsonConvert.SerializeObject(selectors));
		}

		private (bool, object) SelectorsToList(JArray selectors) {
			if (selectors == null) {
				return (false, "SelectorsToList: null selectors array");
            }
			if (selectors.Count == 0) {
				return (false, "SelectorsToList: empty selectors array");
			}
			var selector_list = new List<string>();
			for (int i=0; i < selectors.Count; i++) {
				string selector = null;
				try {
					// NB selectors is a list of one element lists
					selector = (string)(selectors[i][0]);
					selector_list.Add(selector);
				}
				catch (Exception ex) {
					logger.Error($"ChooseSelector: {ex}");
                }
            }
			// logger.Error($"ChooseSelector: no match for prefix[{selector_prefix}] in selectors[{selectors}]");
			return (true, selector_list);
        }
	}
}