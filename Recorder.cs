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
            // NB we also need --user-data-dir option per this MS issue...
            // https://github.com/microsoft/vscode/issues/146410
            // Note that the debug port will only work if there is no prior
            // running instance of msegde.exe. If there is, it will have been
            // started without a debug port, and browser.Start() will just
            // give us another tab, which is a child proc spawned from the
            // parent existing msedge.exe image, which has no debig port.
            // TODO: add code to check for msedge.exe instance, and popup
            // warning....
            browser = new Process();
            browser.StartInfo.FileName = config.EdgePath;
            var args = $"--remote-debugging-port={config.EdgeRecorderPort} --user-data-dir={config.EdgeUserDataDir}";
            $"Recorder browser starting with {config.EdgePath} {args}".Info();
            browser.StartInfo.Arguments = args;
            browser.Start();
            json_list_url = $"http://localhost:{config.EdgeRecorderPort}/json/list";
            $"Recorder browser: Id:{browser.Id}, Handle:{browser.Handle}, Title:{browser.MainWindowTitle}".Info();
        }

        public async Task Start() {
            $"Recorder.Start: awaiting {json_list_url}".Info();
            string json_list = await edge_client.GetStringAsync(json_list_url);
            $"Recorder.Start: json_list:{json_list}".Info();
        }

        public async Task Stop() {
            await Task.Delay(0);
        }
    }
}