using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class StartRecording : ButtonAction
    {
        public StartRecording() { }
        public override void Run()
        {
            $"Starting recorder...".Info();
            // https://learn.microsoft.com/en-us/microsoft-edge/devtools-protocol-chromium/
            // msedge.exe --remote-debugging-port=9222
            // start default browser - or new tab - and point it at 
            // our web server
            // var start_info = new ProcessStartInfo();
            // start_info.Arguments = 
            var browser = new Process();
            browser.StartInfo.FileName = "msedge.exe";
            browser.StartInfo.Arguments = "--remote-debugging-port=9222";
            browser.Start();
        }
    }
}
