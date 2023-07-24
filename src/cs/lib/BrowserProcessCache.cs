using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Collections.Generic;
using PuppeteerSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {

	public class BrowserLaunch {
		
		[JsonProperty("user_data_dir")]
		public string UserDataDir { get; set; }

		[JsonProperty("exe_path")]
		public string ExePath { get; set; }

		[JsonProperty("dev_tools")]
		public bool DevTools { get; set; }

		[JsonProperty("headless")]
		public bool Headless { get; set; }

		// Simplify logging...
		public override string ToString() {
			return JsonConvert.SerializeObject(this);
		}

		// Default ctor: let members default
		public BrowserLaunch() { }

		// Copy ctor for handling script overrides of
		// BrowserLaunch instances defined in config.json
		public BrowserLaunch(BrowserLaunch source_instance) {
			UserDataDir = source_instance.UserDataDir;
			ExePath = source_instance.ExePath;
			DevTools = source_instance.DevTools;
			Headless = source_instance.Headless;
        }
	}

	public class BrowserProcessCache {

		private static readonly Lazy<BrowserProcessCache> lazy =
			new Lazy<BrowserProcessCache>(() => new BrowserProcessCache());
		public static BrowserProcessCache Instance { get { return lazy.Value; } }

		private ConfigHelper config_helper;
		private BizDeckLogger logger;
		int current_port;
		Dictionary<BrowserLaunch, IBrowser> browser_instance_map = new();
		private readonly object browser_instance_map_lock = new object();

		public BrowserProcessCache() {
			logger = new(this);
			config_helper = ConfigHelper.Instance;
			current_port = config_helper.BizDeckConfig.BrowserRecorderPort;
		}

		public async Task<BizDeckResult> GetBrowserInstance(BrowserLaunch bl) {
			// If we already have an instance cached, return it...
			lock (browser_instance_map_lock) {
				if (browser_instance_map.ContainsKey(bl)) {
					return new BizDeckResult(true, browser_instance_map[bl]);
				}
			}
			// ...no cached instance that matches the BrowserLaunch params, so create...
			IBrowser browser = null;
			LaunchOptions launch_options = new();
			int port = NextFreePort();
			launch_options.Args = new string[1] { $"--remote-debugging-port={port}" };
			launch_options.Headless = bl.Headless;
			launch_options.ExecutablePath = bl.ExePath;
			launch_options.Devtools = bl.DevTools;
			string budd = bl.UserDataDir;
			if (!String.IsNullOrWhiteSpace(budd)) {
				// If config has an empty string for browser_user_data_dir, we let it default
				// IF an absolute path has been supplied, we pass it through
				// If it's a relative path, we assume relative to BDROOT
				if (!Path.IsPathRooted(budd)) {
					budd = Path.Combine(config_helper.BDRoot, budd);
				}
				launch_options.UserDataDir = budd;
				logger.Info($"GetBrowserInstance: UserDataDir[{budd}]");
			}
			else {
				logger.Info($"GetBrowserInstance: no browser_user_data_dir in BrowserLaunch, not supplying --user-data-dir");
			}
			// Create browser instance. NB we're not doing ConfigureAwait(false)
			// to enable resumption on another thread, but that is what happens
			// in the logs. So there must be a ConfigureAwait() in PuppeteerSharp.
			try {
				browser = await Puppeteer.LaunchAsync(launch_options);
			}
			catch (Exception ex) {
				string launch_error = $"GetBrowserInstance: browser launch failed: {ex}";
				logger.Error(launch_error);
				return new BizDeckResult(launch_error);
			}
			// hold lock and cache browser instance
			lock (browser_instance_map_lock) {
				browser_instance_map[bl] = browser;
			}
			return new BizDeckResult(true, browser);
		}

		public async Task<BizDeckResult> ReleaseBrowserInstance(BrowserLaunch bl, IBrowser instance) {
			// Why the BrowserLaunch key as well as value? It saves us maintaining a backmap,
			// and makes the browser_instance_map delete logic simpler. We have to lock on that,
			// and since we cannot lock in an async method, we isolate that lock in a sync helper
			// method. 
			if (instance == null || bl == null) {
				string error = $"ReleaseBrowserInstance: null IBrowser or BrowserLaunch";
				logger.Error(error);
				return new BizDeckResult(error);
            }
			if (config_helper.BizDeckConfig.ReuseBrowserInstances) {
				logger.Info($"ReleaseBrowserInstance: reusing {bl}");
				return BizDeckResult.Success;
            }
			try {
				await instance.CloseAsync();
				DeleteBrowserInstance(bl);
				return BizDeckResult.Success;
			}
			catch (Exception ex) {
				string close_error = $"ReleaseBrowserInstance: browser close failed: {ex}";
				logger.Error(close_error);
				return new BizDeckResult(close_error);
			}
		}

		#region InternalMethods
		private int NextFreePort() {
			while (!IsFree(current_port)) {
				current_port += 1;
			}
			return current_port;
		}

		private bool IsFree(int port) {
			// https://stackoverflow.com/questions/53815519/get-random-free-opened-port-for-tests
			IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
			IPEndPoint[] listeners = properties.GetActiveTcpListeners();
			int[] openPorts = listeners.Select(item => item.Port).ToArray<int>();
			return openPorts.All(openPort => openPort != port);
		}

		private bool DeleteBrowserInstance(BrowserLaunch bl) {
			lock (browser_instance_map_lock) {
				if (browser_instance_map.ContainsKey(bl)) {
					browser_instance_map.Remove(bl);
					return true;
                }
			}
			return false;
        }
		#endregion InternalMethods
	}
}
