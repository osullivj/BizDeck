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
		ConfigHelper config_helper;
		BizDeckLogger logger;
		Dictionary<string, Dispatch> dispatchers = new();
		BrowserLaunch browser_launch;
		IBrowser browser;
		IPage current_page;
		JObject pending_viewport_step;
		WaitForSelectorOptions wait_for_selector_options = new();
		List<string> urls_visited = new();
		
		public PuppeteerDriver() {
			logger = new(this);
			config_helper = ConfigHelper.Instance;

			dispatchers["setViewport"] = this.SetViewport;
			dispatchers["navigate"] = this.Navigate;
			dispatchers["click"] = this.Click;
			dispatchers["change"] = this.Change;
			dispatchers["keyDown"] = this.KeyDown;
			dispatchers["keyUp"] = this.KeyUp;
			dispatchers["bdScrape"] = this.Scrape;

			// WaitForSelectorOptions
			wait_for_selector_options.Hidden = false;
			wait_for_selector_options.Timeout = config_helper.BizDeckConfig.HttpGetTimeout;
			wait_for_selector_options.Visible = true;
		}

		public async Task<BizDeckResult> PlaySteps(string name, JObject chrome_recording) {
			// Clear state left over from previous PlaySteps
			current_page = null;
			pending_viewport_step = null;
			JArray steps = null;
			string title = null;

			try {
				// Create browser instance. NB we're not doing ConfigureAwait(false)
				// to enable resumption on another thread, but that is what happens
				// in the logs. So there must be a ConfigureAwait() in PuppeteerSharp.
				browser_launch = BuildBrowserLaunch(chrome_recording);
				BizDeckResult result = await BrowserProcessCache.Instance.GetBrowserInstance(browser_launch);
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
						step_result = await dispatchers[step_type](step);
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
				string play_error = $"PlaySteps: browser script failed: {ex}";
				logger.Error(play_error);
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
					await current_page.SetViewportAsync(vpo);
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
				// ConfigureAwait(): P# must fo it internally.
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
			logger.Info($"Navigate: GoToAsync({url})");
			try {
				http_response = await current_page.GoToAsync(url);
			}
			catch (Exception ex) {
				logger.Error($"Navigate: NewPageAsync url[{url}] {ex}");
				return new BizDeckResult(ex.Message);
			}
			if (http_response.Status != System.Net.HttpStatusCode.OK) {
				error = $"bad status[{http_response.StatusText}] for url[{url}]";
				logger.Error($"Navigate: {error}");
				return new BizDeckResult(error);
            }
			// At the point we know the navigation to url was succesful
			urls_visited.Add(url);
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
			IElementHandle element = null;
			foreach (var selector in selector_list) {
				try {
					// See CustomQueriesManager.GetQueryHandlerAndSelector() in P#
					// The method potentially throws an exception, if we don't 
					// handle here it causes deadlock
					element = await current_page.QuerySelectorAsync(selector);
					if (element != null) {
						return new BizDeckResult(true, element);
                    }
				}
				catch (Exception ex) {
					// Selector misses are routine. We follow the Chrome Recorder playback
					// practice of trying all the selectors in turn. Switch on debug
					// logging to see the misses.
					logger.Debug($"QuerySelectorAsync: selector[{selector}], {ex}");
                }
			}
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
					selector_list.Add(selector);
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