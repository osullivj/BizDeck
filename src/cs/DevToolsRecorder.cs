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
using Microsoft.Playwright;

namespace BizDeck
{
    public class DevToolsRecorder : IRecorder
    {
        private ConfigHelper config_helper;
        private HttpClient edge_client;
        private Process browser;
        private string json_list_url;
        private string browser_cmd_line_args;

        private List<ClientWebSocket> debug_websock_list;
        private List<byte[]> debug_websock_buffer_list;

        public DevToolsRecorder(ConfigHelper ch)
        {
            edge_client = new HttpClient();
            config_helper = ch;
            browser_cmd_line_args = $"--remote-debugging-port={config_helper.BizDeckConfig.EdgeRecorderPort} "
                                            + $" --user-data-dir={config_helper.GetFullLogPath()}";
            json_list_url = $"http://localhost:{config_helper.BizDeckConfig.EdgeRecorderPort}/json/list";
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
            browser.StartInfo.FileName = config_helper.BizDeckConfig.EdgePath;
            $"Recorder browser starting with {config_helper.BizDeckConfig.EdgePath} {browser_cmd_line_args}".Info();
            browser.StartInfo.Arguments = browser_cmd_line_args;
            browser.Start();
            $"Recorder browser: Id:{browser.Id}, Handle:{browser.Handle}".Info();
            // Now tee up two tasks: one to await the /json/list result from the
            // debugger port, and one to timeout. If the timeout completes first
            // we know that the edge instance launched here was not the first, and
            // that the pre-existing instance is running without a debug port.
            var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.EdgeJsonListTimeout));
            debug_websock_list = new List<ClientWebSocket>();
            debug_websock_buffer_list = new List<byte[]>();
            try
            {
                var json_list_str = await edge_client.GetStringAsync(json_list_url,
                                                        http_cancel_token_source.Token);
                $"Recorder browser: json/list:{json_list_str}".Info();
                // Now we have a list from DevTools, so we deserialize,
                // and connect to each of the debug websocket URLs. 
                List<DevToolsJsonListResponse> json_list_arr = JsonSerializer.Deserialize<List<DevToolsJsonListResponse>>(json_list_str);
                var task_list = new List<Task>(json_list_arr.Count);
                foreach (DevToolsJsonListResponse response in json_list_arr)
                {
                    var websock = new ClientWebSocket();
                    var ws_connect_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.EdgeJsonListTimeout));
                    task_list.Add(websock.ConnectAsync(new System.Uri(response.WebSocketDebuggerUrl), ws_connect_cancel_token_source.Token));
                    debug_websock_list.Add(websock);
                    debug_websock_buffer_list.Add(new byte[1024]);
                }
                // Wait on the connection tasks
                await Task.WhenAll(task_list);
                $"Recorder /json/list complete".Info();
                // Should be connected to each websock now, so start listening
                await ReceiveAsync();
            }
            catch (OperationCanceledException ex) {
                $"Recorder /json/list timeout".Error();
                // TODO: add code here to redirect the browser to an
                // error page about msedge.exe instances.
            }
        }

        public async Task ReceiveAsync()
        {
            // TODO: how to cancel or close the streams on stop recording
            // or ctrl-C or error
            $"Recorder. /json/list complete: {debug_websock_list.Count} debug sessions".Info();
            var task_list = new List<Task<WebSocketReceiveResult>>(debug_websock_list.Count);
            // Build a task list with sync invoke of async meths to get hold
            // of the Task object. Google Stephen Cleary's blog or read his
            // book on C# concurrrency. With the task list we can use
            // await Task.WhenAny to handle all the sockets in on place.
            for (int inx = 0; inx < debug_websock_list.Count; inx++)
            {
                var websock = debug_websock_list[inx];
                var buffer = debug_websock_buffer_list[inx];
                var receive_cancel_token_source = new CancellationTokenSource();
                var task = websock.ReceiveAsync(buffer, receive_cancel_token_source.Token);
                task_list.Add(task);
            }
            $"Recorder. task_list: {task_list.Count} tasks".Info();
            WebSocketReceiveResult recv_result;
            var when_any_cancel_token_source = new CancellationTokenSource();
            Task<WebSocketReceiveResult> recv_task;
            // TODO: how do we break out of this loop?
            // Can we use a cancel token that somehow gets signalled from Stop() ?
            while (true)
            {
                $"Recorder. awaiting task_list".Info();
                // TODO: line above is last logged - why are we blocked here?
                recv_task = await Task<WebSocketReceiveResult>.WhenAny(task_list).ConfigureAwait(false);
                recv_result = recv_task.Result;
                $"Recorder recv_result:{recv_result.ToString()}".Info();
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