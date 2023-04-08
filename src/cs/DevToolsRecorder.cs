using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class Recorder
    {
        private HttpClient edge_client;
        private Process browser;
        private string json_list_url;
        private string browser_cmd_line_args;
        private BizDeckConfig config;

        public Recorder(BizDeckConfig cfg)
        {
            config = cfg;
            edge_client = new HttpClient();
            browser_cmd_line_args = $"--remote-debugging-port={config.EdgeRecorderPort} "
                                            + $" --user-data-dir={config.EdgeUserDataDir}";
            json_list_url = $"http://localhost:{config.EdgeRecorderPort}/json/list";
            browser = null;
        }

        public async Task StartBrowser()
        { 
            if (browser != null)
            {
                $"Recorder browser already running: Id:{browser.Id}, Handle:{browser.Handle}".Info();
                return;
            }
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
            $"Recorder browser starting with {config.EdgePath} {browser_cmd_line_args}".Info();
            browser.StartInfo.Arguments = browser_cmd_line_args;
            browser.Start();
            $"Recorder browser: Id:{browser.Id}, Handle:{browser.Handle}".Info();
            // Now tee up two tasks: one to await the /json/list result from the
            // debugger port, and one to timeout. If the timeout completes first
            // we know that the edge instance launched here was not the first, and
            // that the pre-existing instance is running without a debug port.
            var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config.EdgeJsonListTimeout));
            try
            {
                var json_list_str = await edge_client.GetStringAsync(json_list_url,
                                                        http_cancel_token_source.Token);
                $"Recorder browser: json/list:{json_list_str}".Info();
                // Now we have a list from Dev Tools, so we deserialize,
                // and connect to each of the debug websocket URLs. 
                List<DevToolsJsonListResponse> json_list_arr = JsonSerializer.Deserialize<List<DevToolsJsonListResponse>>(json_list_str);
                var task_list = new List<Task>(json_list_arr.Count);
                foreach (DevToolsJsonListResponse response in json_list_arr)
                {
                    var websock = new ClientWebSocket();
                    var ws_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config.EdgeJsonListTimeout));
                    task_list.Add(websock.ConnectAsync(new System.Uri(response.WebSocketDebuggerUrl), ws_cancel_token_source.Token));
                }
                // Wait on the connection tasks
                await Task.WhenAll(task_list);
                // Should be connected to each websock now, so start listening
                // TODO: invoke ReceiveAsync
            }
            catch (OperationCanceledException ex) {
                $"Recorder /json/list timeout".Error();
                // TODO: add code here to redirect the browser to an
                // error page about msedge.exe instances.
            }
        }

        public async Task Start() {
            await StartBrowser();
        }

        public async Task Stop() {
            await Task.Delay(0);
        }
    }
}