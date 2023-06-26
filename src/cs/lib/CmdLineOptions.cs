using System;
using System.IO;
using System.Linq;
using CommandLine;

namespace BizDeck
{
	public class CmdLineOptions {
		// BizDeck dir tree always has the same structure eg BizDeck/cfg
		// The variants are BDROOT and config file name. When we override
		// the default config path, we derive ConfigDir from that path.

		private string _bdroot_default = Path.Combine(new string[] {
							Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
							"BizDeck"});
		private string _config_dir_default;
		private string _config_path_default;
		private string _config_path;

		public CmdLineOptions() {
			_config_dir_default = Path.Combine(_bdroot_default, "cfg");
			_config_path_default = Path.Combine(_config_dir_default, "config.json");
		}

		// No CommandLine [Option(...)] here as these are derived from
		// config_path
		public string config_dir { get; private set; }
		public string bdroot { get; private set; }

		[Option('c', "config", Required = false, HelpText = "Config path.")]
		public string config_path {
			get {
				if (String.IsNullOrEmpty(_config_path)) {
					bdroot = _bdroot_default;
					config_dir = _config_dir_default;
					return _config_path_default;
				}
				string[] config_path_elems = _config_path.Split(Path.DirectorySeparatorChar);
				var config_dir_path_elems = config_path_elems.Take<string>(config_path_elems.Length - 1);
				var bdroot_path_elems = config_path_elems.Take<string>(config_path_elems.Length - 2);
				bdroot = Path.Combine(bdroot_path_elems.ToArray());
				config_dir = Path.Combine(config_dir_path_elems.ToArray());
				return _config_path;
			}
			set { _config_path = value; }
		}

		// static helper method for Server.cs or unit tests loading config given
		// string[] args from Main or test cmd line params
		public static BizDeckResult InitAndLoadConfigHelper(string[] args) {
			var parser = new Parser();
			var result = parser.ParseArguments<CmdLineOptions>(args);
			// Create and init config singleton
			ConfigHelper.Instance.Init(result.Value);
			return ConfigHelper.Instance.LoadConfig();
		}
	}
}

