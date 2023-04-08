using System;
using CommandLine;

namespace BizDeck
{
	public class CmdLineOptions
	{
		private string _bdroot_default = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		private string _bdroot;
		[Option('b', "bdroot", Required = false, HelpText = "BizDeck root dir.")]
		public string bdroot {
			get {
				if (String.IsNullOrEmpty(_bdroot)) { return _bdroot_default; }
				return _bdroot;
			}
			set { _bdroot = value; }
		}
	}
}

