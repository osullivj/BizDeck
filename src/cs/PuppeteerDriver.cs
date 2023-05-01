using System;
using System.Threading.Tasks;
using PuppeteerSharp;


namespace BizDeck
{

	public class PuppeteerDriver
	{
		LaunchOptions launch_options = new();
		ConfigHelper config_helper;
		public PuppeteerDriver(ConfigHelper ch)
		{
			config_helper = ch;
			launch_options.Headless = false;
			launch_options.ExecutablePath = config_helper.BizDeckConfig.BrowserPath;
			launch_options.UserDataDir = config_helper.GetFullLogPath();
			launch_options.Devtools = false;
			launch_options.Args = new string[1] { $"--remote-debugging-port={config_helper.BizDeckConfig.BrowserRecorderPort}" };
		}

		public async Task PlaySteps(string steps)
        {
			var browser = await Puppeteer.LaunchAsync(launch_options);
		}
	}
}