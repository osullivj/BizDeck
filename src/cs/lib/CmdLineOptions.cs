using System;
using CommandLine;

namespace BizDeck
{
	public class CmdLineOptions
	{
		private string _appdata_default = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		private string _appdata;
		[Option('a', "appdata", Required = false, HelpText = "AppData root dir.")]
		public string appdata {
			get {
				if (String.IsNullOrEmpty(_appdata)) { return _appdata_default; }
				return _appdata;
			}
			set { _appdata = value; }
		}

		// static helper method for Server.cs or unit tests loading config given
		// string[] args from Main or test cmd line params
		public static BizDeckConfig InitAndLoadConfigHelper(string[] args) {
			var parser = new Parser();
			var result = parser.ParseArguments<CmdLineOptions>(args);
			// Create and init config singleton
			ConfigHelper.Instance.Init(result.Value);
			return ConfigHelper.Instance.LoadConfig();
		}
	}
}

