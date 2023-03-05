using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class Recorder
    {
        private HttpClient edge_client;
        private Process browser;
        private string json_list_url;
        private BizDeckConfig config;
        public Recorder(BizDeckConfig cfg)
        {
            config = cfg;
            edge_client = new HttpClient();

            // https://learn.microsoft.com/en-us/microsoft-edge/devtools-protocol-chromium/
            // msedge.exe --remote-debugging-port=9222
            // start default browser - or new tab - and point it at 
            // our web server
            browser = new Process();
            browser.StartInfo.FileName = config.EdgePath;
            var args = $"--remote-debugging-port={config.EdgeRecorderPort}";
            $"Recorder starting with {args}".Info();
            browser.StartInfo.Arguments = args;
            browser.Start();
            json_list_url = $"http://localhost:{config.EdgeRecorderPort}/json/list";
        }

        public async Task Start() {
            string json_list = await edge_client.GetStringAsync(json_list_url);
            $"Recorder.Start: json_list:{json_list}".Info();
        }

        public async Task Stop() {
            await Task.Delay(0);
        }
    }
}