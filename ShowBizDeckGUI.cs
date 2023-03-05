using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class ShowBizDeckGUI:ButtonAction
    {
        private string url;
        public ShowBizDeckGUI(string url) { this.url = url; }
        public override void Run() {
            $"Starting browser for {url}".Info();
            // start default browser - or new tab - and point it at 
            // our web server
            var browser = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }
            };
            browser.Start();
        }

        public async override Task RunAsync()
        {
            Run();
            await Task.Delay(0);
        }
    }
}
