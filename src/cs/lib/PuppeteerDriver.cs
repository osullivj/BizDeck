using System;
using System.IO;
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

// Major assumption: the nature of the Chrome Recorder steps we're playing
// mean we will have one browser, one page in play at a time. There may be more
// than one Puppeteer Driver instance, but they could contend over a browser
// instance if actions are deck triggered and web api triggered simultaneously.
// We accept this limitation protem as it will make it easier to stack urls
// that are navigation targets, and use them as secrets look up keys so we can
// reove user names and passwords from steps.
namespace BizDeck {

	public delegate Task<BizDeckResult> Dispatch(JObject step);
		// define this driver's own callback dispatch signature
		// for handling Chrome DevTools Recorder steps

	public class PuppeteerDriver {
		// Use of Lazy<T> gives us a thread safe singleton
		// Instance property is the access point
		// https://csharpindepth.com/articles/singleton
		private static readonly Lazy<PuppeteerDriver> lazy =
			new Lazy<PuppeteerDriver>(() => new PuppeteerDriver());
		public static PuppeteerDriver Instance { get { return lazy.Value; } }

		ConfigHelper config_helper;
		BizDeckLogger logger;
		Dictionary<string, Dispatch> dispatchers = new();
		BrowserLaunch browser_launch;
		IBrowser browser;
		IPage current_page;
		JObject pending_viewport_step;
		WaitForSelectorOptions wait_for_selector_options = new();
		Stack<string> url_stack = new();
		TracingOptions tracing_options = new();
		
		private PuppeteerDriver() {
			logger = new(this);
			config_helper = ConfigHelper.Instance;

			dispatchers["setViewport"] = this.SetViewport;
			dispatchers["navigate"] = this.Navigate;
			dispatchers["click"] = this.Click;
			dispatchers["change"] = this.Change;
			dispatchers["keyDown"] = this.KeyDown;
			dispatchers["keyUp"] = this.KeyUp;
			dispatchers["bdScrape"] = this.Scrape;
			dispatchers["bdPause"] = this.Pause;
			dispatchers["bdPushURL"] = this.PushURL;
			dispatchers["bdPopURL"] = this.PopURL;

			// WaitForSelectorOptions
			wait_for_selector_options.Hidden = false;
			wait_for_selector_options.Timeout = config_helper.BizDeckConfig.HttpGetTimeout;
			wait_for_selector_options.Visible = true;
		}

		private string NewFileName(string basename) {
			char suffix = 'a';
			bool file_exists = true;
			string file_name, path = "";
			var timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
			while (file_exists) {
				file_name = $"bd_{basename}_{timestamp}_r{suffix}.json";
				path = Path.Combine(ConfigHelper.Instance.LogDir, file_name);
				file_exists = File.Exists(path);
				suffix++;
			}
			return path;
		}

		public async Task<BizDeckResult> PlaySteps(string name, JObject chrome_recording) {
			// Clear state left over from previous PlaySteps
			pending_viewport_step = null;
			JArray steps = null;
			string title = null;
			string play_error = null;

			try {
				// Create browser instance. NB we're not doing ConfigureAwait(false)
				// to enable resumption on another thread, but that is what happens
				// in the logs. So there must be a ConfigureAwait() in PuppeteerSharp.
				browser_launch = BuildBrowserLaunch(chrome_recording);
				if (browser_launch.Tracing) {
					tracing_options.Screenshots = browser_launch.Screenshots;
					tracing_options.Categories = browser_launch.Categories;
                }
				BizDeckResult result = await BrowserProcessCache.Instance.GetBrowserInstance(browser_launch).ConfigureAwait(true);
				if (!result.OK) {
					return result;
                }
				browser = (IBrowser)result.Payload;
			}
			catch (Exception ex) {
				string launch_error = $"PlaySteps: browser launch failed: {ex}";
				logger.Error(launch_error);
				return new BizDeckResult(launch_error);
            }
			try {
				steps = (JArray)chrome_recording["steps"];
				title = (string)chrome_recording["title"];
				logger.Info($"PlaySteps: playing[{title}], {steps.Count} steps");
				string error = null;
				int step_index = 0;
				BizDeckResult step_result = null;
				foreach (JObject step in steps) {
					string step_type = (string)step.GetValue("type");
					logger.Info($"PlaySteps: playing step {step_index}, type[{step_type}]");
					if (dispatchers.ContainsKey(step_type)) {
						step_result = await dispatchers[step_type](step).ConfigureAwait(true);
						if (!step_result.OK) {
							error = $"step {step_index}, type[{step_type}], result[{step_result}]";
							logger.Error($"PlaySteps: played {error}");
							return new BizDeckResult(error);
						}
						else {
							logger.Info($"PlaySteps: played step {step_index}, type[{step_type}], result[{step_result}]");
						}
					}
					else {
						logger.Error($"PlaySteps: skipping unknown step_type[{step_type}]");
					}
					step_index++;
				}
			}
			catch (Exception ex) {
				play_error = $"PlaySteps: browser script failed: {ex}";
				logger.Error(play_error);
				return new BizDeckResult(play_error);
			}
			await BrowserProcessCache.Instance.ReleaseBrowserInstance(browser_launch, browser).ConfigureAwait(true);
			if (play_error != null) {
				return new BizDeckResult(play_error);
			}
			return BizDeckResult.Success;
		}

        #region StepsMethods
        public async Task<BizDeckResult> SetViewport(JObject step)
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
					await current_page.SetViewportAsync(vpo).ConfigureAwait(true);
				}
				catch (Exception ex) {
					logger.Error($"SetViewport: failed {ex}");
					return new BizDeckResult(ex.Message);
				}
			}
			return BizDeckResult.Success;
        }

		public async Task<BizDeckResult> Navigate(JObject step) {
			string error = null;
			IResponse http_response = null;
			string url = (string)step["url"];
			if (current_page == null) {
				logger.Info($"Navigate: NewPageAsync for url[{url}]");
				// This will resume on another thread, despite no
				// ConfigureAwait(): P# must do it internally.
				try {
					current_page = await browser.NewPageAsync();
				}
				catch (Exception ex) {
					logger.Error($"Navigate: NewPageAsync failed {ex}");
					return new BizDeckResult(ex.Message);
				}
				if (pending_viewport_step != null) {
					BizDeckResult set_viewport_result = await SetViewport(pending_viewport_step);
					pending_viewport_step = null;
					if (!set_viewport_result.OK) {
						return set_viewport_result;
					}
                }
			}
			if (url.Contains("chrome:")) {
				logger.Info($"Navigate: skipping internal chrome url {url}");
				return BizDeckResult.Success;
            }
			// NB the HTML should be fully rendered when GoToAsync returns
			// But it may be possible that JS will causes elements to
			// render aftrwards...
			WaitUntilNavigation nav_wait = WaitUntilNavigation.Load;
			if (step.ContainsKey("bd_wait")) {
				Enum.TryParse<WaitUntilNavigation>((string)step["bd_wait"], out nav_wait);
            }
			logger.Info($"Navigate: GoToAsync({url}, {nav_wait})");
			try {
				if (browser_launch.Tracing) {
					tracing_options.Path = NewFileName("trace");
					await current_page.Tracing.StartAsync(tracing_options);
				}
				// https://stackoverflow.com/questions/65971972/puppeteer-sharp-get-html-after-js-finished-running
				http_response = await current_page.GoToAsync(url, nav_wait);
				if (browser_launch.Tracing) {
					await current_page.Tracing.StopAsync();
					var metrics_dict = await current_page.MetricsAsync();
					var metrics_text = String.Join(Environment.NewLine, metrics_dict);
					var metrics_path = NewFileName("metrics");
					File.WriteAllText(metrics_path, metrics_text);
				}
			}
			catch (Exception ex) {
				logger.Error($"Navigate: NewPageAsync url[{url}] {ex}");
				return new BizDeckResult(ex.Message);
			}
			// http_response can be null when we invoke Navigate from PopURL
			if (http_response != null && http_response.Status != System.Net.HttpStatusCode.OK) {
				error = $"bad status[{http_response.StatusText}] for url[{url}]";
				logger.Error($"Navigate: {error}");
				return new BizDeckResult(error);
            }
			// At the point we know the navigation to url was succesful
			return BizDeckResult.Success;
        }

		public async Task<BizDeckResult> Click(JObject step)
        {
			JArray selectors = (JArray)step["selectors"];
			try { 
				BizDeckResult selector_result = await QuerySelectorListAsync(selectors);
				if (!selector_result.OK) {
					return selector_result;
                }
				IElementHandle element_handle = (IElementHandle)selector_result.Payload;
				await element_handle.ClickAsync();
			}
			catch (Exception ex) {
				logger.Error($"Click: {ex}");
				return new BizDeckResult(ex.Message);
            }
			return BizDeckResult.Success;
        }

		public async Task<BizDeckResult> Change(JObject step) {
			// This is a change step, so we may be type a user name or password
			// into an edit field. Fortunately the selectors know about the type
			// of input field. In that case QuerySelectorListAsync will use
			// the third part of the tuple to signal a secrets key
			JArray selectors = (JArray)step["selectors"];
			string new_value = (string)step["value"];
			// could the value be a secrets cache ref?
			// screts cache?
			BizDeckResult resolve_result = NameStack.Instance.Resolve(new_value);
			if (resolve_result.OK) {
				logger.Info($"Change: {new_value} resolved in NameStack");
				new_value = resolve_result.Message;
			}
			else {
				logger.Info($"Change: {new_value} not resolved in NameStack");
			}
			BizDeckResult selector_result = await QuerySelectorListAsync(selectors);
			if (!selector_result.OK) {
				return selector_result;
            }
			string extra_value = "";
			if (step.ContainsKey("bd_extra_value")) {
				extra_value = (string)step["bd_extra_value"];
			}
			try {
				IElementHandle element_handle = (IElementHandle)selector_result.Payload;
				await element_handle.TypeAsync(new_value + extra_value);
			}
			catch (Exception ex) {
				logger.Error($"Change: {ex}");
				return new BizDeckResult(ex.Message);
            }
			return BizDeckResult.Success;
		}

		public async Task<BizDeckResult> KeyDown(JObject step) {
			string key = null;
			try {
				key = (string)step["key"];
				await current_page.Keyboard.DownAsync(key);
			}
			catch (Exception ex) {
				logger.Error($"KeyDown: key[{key}] {ex}");
				return new BizDeckResult(ex.Message);
			}
			return BizDeckResult.Success;
		}

		public async Task<BizDeckResult> KeyUp(JObject step) {
			string key = null;
			try {
				key = (string)step["key"];
				await current_page.Keyboard.UpAsync(key);
			}
			catch (Exception ex) {
				logger.Error($"KeyUp: key[{key}] {ex}");
				return new BizDeckResult(ex.Message);
			}
			return BizDeckResult.Success;
		}

		public async Task<BizDeckResult> Scrape(JObject step) {
			if (current_page == null) {
				logger.Error($"Scrape: no current page for {step}");
				return BizDeckResult.NoCurrentPage;
            }
			string root_xpath = null;
			string cache_group = null;
			string cache_key = null;
			JObject field_mappings = null;
			try {
				root_xpath = (string)step["bd_root_xpath"];
				cache_group = (string)step["bd_cache_group"];
				cache_key = (string)step["bd_cache_key"];
				field_mappings = (JObject)step["bd_field_mappings"];
				IElementHandle[] handle_array = await current_page.XPathAsync(root_xpath);
				logger.Info($"Scrape: {root_xpath} yields {handle_array.Length} elements");
				// The root_xpath may resolve to several children. Extract all the fields
				// from each child
				List<Dictionary<string, string>> cache_values = new(handle_array.Length);
				List<string> cache_column_names = new(field_mappings.Count);
				foreach (var handle in handle_array) {
					Dictionary<string, string> cache_row = new();
					foreach (var pair in field_mappings) {
						string xpath_rel = pair.Key;
						string scrape_js = null;
						JObject scrape_spec = pair.Value as JObject;
						string scrape_js_key = (string)scrape_spec["bd_scraper_js"];
						if (config_helper.Scrapers.ContainsKey(scrape_js_key)) {
							scrape_js = config_helper.Scrapers[scrape_js_key];
                        }
						else {
							logger.Error($"Scrape: {scrape_js_key} scraper key did not resolve");
							continue;
						}
						string cache_field = (string)scrape_spec["bd_cache_field"];
						// This if will only eval true on the first time round
						// this inner loop, so we don't repeat the cache_column_names
						// population for each row...
						if (cache_column_names.Count < field_mappings.Count) {
							cache_column_names.Add(cache_field);
                        }
						// Now let's exec the scrape JS to get the field value
						// First we need to resolve xpath_rel, then we run the JS
						var child_handle_array = await handle.XPathAsync(xpath_rel);
						if (child_handle_array == null || child_handle_array.Length == 0) {
							logger.Error($"Scrape: {xpath_rel} rel path did not resolve");
							continue;
                        }
						if (child_handle_array.Length > 1) {
							logger.Info($"Scrape: {xpath_rel} resolves to {child_handle_array.Length} elements");
                        }
						string val = await child_handle_array[0].EvaluateFunctionAsync<string>(scrape_js);
						cache_row[cache_field] = val;
					}
					cache_values.Add(cache_row);
                }
				DataCache.Instance.Insert(cache_group, cache_key, cache_values, cache_column_names);
			}
			catch (Exception ex) {
				logger.Error($"Scrape: {ex}");
				return new BizDeckResult(ex.Message);
			}
			return BizDeckResult.Success;
		}

		public async Task<BizDeckResult> Pause(JObject step) {
			if (!step.ContainsKey("delay_ms")) {
				logger.Error($"Pause: missing delay_ms in {step}");
				return BizDeckResult.PauseMissingDelay;
			}
			int delay_ms = (int)step["delay_ms"];
			await Task.Delay(delay_ms);
			return BizDeckResult.Success;
		}

		public async Task<BizDeckResult> PushURL(JObject step) {
			if (current_page == null) {
				logger.Error($"PushURL: no current page for {step}");
				return BizDeckResult.NoCurrentPage;
			}
			var jurl = await current_page.EvaluateExpressionAsync("window.location.href");
			var url = jurl.ToString();
			url_stack.Push(url);
			logger.Info($"PushURL: {url}");
			return new BizDeckResult(true, url);
		}

		public async Task<BizDeckResult> PopURL(JObject step) {
			if (url_stack.Count == 0) {
				logger.Error($"PopURL: empty stack!");
				return BizDeckResult.EmptyURLStack;
			}
			var url = url_stack.Pop();
			// We're going to use the Navigate method, which will look for
			// bd_wait in the step. So we check our PopURL step for bd_wait.
			JObject navigate = new JObject();
			navigate["url"] = url;
			if (step.ContainsKey("bd_wait")) {
				navigate["bd_wait"] = step["bd_wait"];
			}
			logger.Info($"PopURL: navigating to {url}");
			return await Navigate(navigate);
		}

		#endregion StepsMethods

		#region InternalMethods
		private async Task<BizDeckResult> QuerySelectorListAsync(JArray selectors) {
			if (current_page == null) {
				return BizDeckResult.NoCurrentPage;
            }
			BizDeckResult selectors_result = SelectorsToList(selectors);
			if (!selectors_result.OK) {
				return selectors_result;
            }

			List<string> selector_list = (List<string>)selectors_result.Payload;
			var selectors_s = $"selectors<{String.Join("|", selector_list)}>";
			logger.Info($"QuerySelectorAsync: {selectors_s}");
			IElementHandle element = null;
			foreach (var selector in selector_list) {
				try {
					// See CustomQueriesManager.GetQueryHandlerAndSelector() in P#
					// The method potentially throws an exception, if we don't 
					// handle here it causes deadlock
					element = await current_page.QuerySelectorAsync(selector);
					if (element != null) {
						logger.Info($"QuerySelectorAsync: hit {selector}");
						return new BizDeckResult(true, element);
                    }
				}
				catch (Exception ex) {
					// Selector misses are routine. We follow the Chrome Recorder playback
					// practice of trying all the selectors in turn. Switch on debug
					// logging to see the misses.
					logger.Error($"QuerySelectorAsync: selector[{selector}], {ex}");
                }
			}
			// Selectors can fail to resolve without generating an Exception, so log here
			// on the misses
			logger.Error($"QuerySelectorAsync: no hits for selectors: {selectors_s}");
			return BizDeckResult.NoSelectorResolves;
		}

		private BizDeckResult SelectorsToList(JArray selectors) {
			if (selectors == null) {
				return BizDeckResult.NullSelectorArray;
            }
			if (selectors.Count == 0) {
				return BizDeckResult.EmptySelectorArray;
			}
			var selector_list = new List<string>();
			for (int i=0; i < selectors.Count; i++) {
				string selector = null;
				try {
					// NB selectors is a list of one element lists
					selector = (string)(selectors[i][0]);
					BizDeckResult ir = NameStack.Instance.Interpolate(selector);
					if (ir.OK) {
						selector_list.Add(ir.Message);
					}
					else {
						logger.Error($"ChooseSelector: selector({selector}) did not interpolate");
					}
				}
				catch (Exception ex) {
					logger.Error($"ChooseSelector: {ex}");
                }
            }
			return new BizDeckResult(true, selector_list);
        }

		private BrowserLaunch BuildBrowserLaunch(JObject script) {
			string browser_key = config_helper.BizDeckConfig.DefaultBrowser;
			if (script.ContainsKey("bd_browser")) {
				string override_browser_key = (string)script["bd_browser"];
				if (!config_helper.BizDeckConfig.BrowserMap.ContainsKey(override_browser_key)) {
					logger.Error($"BuildBrowserLaunch: browser override[{override_browser_key}] not in BrowserMap");
				}
				else {
					logger.Info($"BuildBrowserLaunch: script browser override[{override_browser_key}]");
					browser_key = override_browser_key;
				}
			}
			// copy config BrowserLaunch as we may need to modify DevTools and Headless
			BrowserLaunch bl = new BrowserLaunch(config_helper.BizDeckConfig.BrowserMap[browser_key]);
			if (script.ContainsKey("bd_dev_tools")) {
				bl.DevTools = (bool)script["bd_dev_tools"];
				logger.Info($"BuildBrowserLaunch: script dev_tools override[{bl.DevTools}]");
			}
			if (script.ContainsKey("bd_headless")) {
				bl.Headless = (bool)script["bd_headless"];
				logger.Info($"BuildBrowserLaunch: script headless override[{bl.Headless}]");
			}
			return bl;
		}

        #endregion InternalMethods
    }
}