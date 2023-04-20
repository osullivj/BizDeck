using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
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

        private ClientWebSocket debug_websock;
        private byte[] debug_websock_buffer = new Byte[8192];
        public DevToolsRecorder(ConfigHelper ch)
        {
            edge_client = new HttpClient();
            config_helper = ch;
            browser_cmd_line_args = $"--remote-debugging-port={config_helper.BizDeckConfig.EdgeRecorderPort} "
                                            + $" --user-data-dir={config_helper.GetFullLogPath()}";
            json_list_url = $"http://localhost:{config_helper.BizDeckConfig.EdgeRecorderPort}/json/list";
            browser = null;
        }

        public bool StartBrowser()
        {
            if (browser != null)
            {
                $"Recorder browser already running: Id:{browser.Id}, Handle:{browser.Handle}".Info();
                return false;
            }
            // https://learn.microsoft.com/en-us/microsoft-edge/devtools-protocol-chromium/
            // msedge.exe --remote-debugging-port=9222
            // NB we also need --user-data-dir option per this MS issue...
            // https://github.com/microsoft/vscode/issues/146410
            // Note that the debug port will only work if there is no prior
            // running instance of msegde.exe. If there is, it will have been
            // started without a debug port, and browser.Start() will just
            // give us another tab, which is a child proc spawned from the
            // parent existing msedge.exe image, which has no debug port.
            // TODO: add code to check for msedge.exe instance, and popup
            // warning....
            browser = new Process();
            browser.StartInfo.FileName = config_helper.BizDeckConfig.EdgePath;
            $"Recorder browser starting with {config_helper.BizDeckConfig.EdgePath} {browser_cmd_line_args}".Info();
            browser.StartInfo.Arguments = browser_cmd_line_args;
            browser.Start();
            $"Recorder browser: Id:{browser.Id}, Handle:{browser.Handle}".Info();
            return true;
        }

        public bool HasBrowser()
        {
            if (browser == null)
                return false;
            return true;
        }

        public async Task StartRecording() { 
            // Now tee up two tasks: one to await the /json/list result from the
            // debugger port, and one to timeout. If the timeout completes first
            // we know that the edge instance launched here was not the first, and
            // that the pre-existing instance is running without a debug port.
            var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.EdgeJsonListTimeout));
            debug_websock = new ClientWebSocket();
            var ws_connect_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.EdgeJsonListTimeout));
            try
            {
                var json_list_str = await edge_client.GetStringAsync(json_list_url,
                                                        http_cancel_token_source.Token);
                $"Recorder browser: json/list:{json_list_str}".Info();
                // Now we have a list from DevTools, so we deserialize,
                // and connect to each of the debug websocket URLs. 
                List<DevToolsJsonListResponse> json_list_arr = JsonSerializer.Deserialize<List<DevToolsJsonListResponse>>(json_list_str);
                var task_list = new List<Task>(json_list_arr.Count);
                DevToolsJsonListResponse real_tab_response = null;
                foreach (DevToolsJsonListResponse response in json_list_arr)
                {
                    if (response.Url.Contains("edge"))
                    {
                        // ignore the websocks for edge:// connections as well as https://edgeservices.bing.com/edges
                        continue;
                    }
                    else
                    {
                        real_tab_response = response;
                        break;
                    }
                }
                if (real_tab_response == null)
                {
                    $"DevToolsRecorder.StartBrowser: no candidate websock found".Error();
                    // TODO: signal error to user, terminate recording session
                    return;
                }
                else
                {
                    $"DevToolsRecorder.StartBrowser: connecting to {real_tab_response.WebSocketDebuggerUrl} for {real_tab_response.Url}".Info();
                }
                await debug_websock.ConnectAsync(new System.Uri(real_tab_response.WebSocketDebuggerUrl), ws_connect_cancel_token_source.Token);
                // Should be connected to each websock now, so start listening
                await ReceiveAsync();
            }
            catch (OperationCanceledException ex) {
                $"Recorder /json/list timeout:{ex.ToString()}".Error();
                // TODO: add code here to redirect the browser to an
                // error page about msedge.exe instances.
            }
        }

        public async Task ReceiveAsync()
        {
            // TODO: how to cancel or close the streams on stop recording
            // or ctrl-C or error
            var receive_cancel_token_source = new CancellationTokenSource();
            WebSocketReceiveResult recv_result = null;
            ArraySegment<Byte> seg_buffer = new ArraySegment<byte>(debug_websock_buffer);
            // var when_any_cancel_token_source = new CancellationTokenSource();
            // TODO: how do we break out of this loop?
            // Can we use a cancel token that somehow gets signalled from Stop() ?
            while (debug_websock.State == WebSocketState.Open)
            {
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        recv_result = await debug_websock.ReceiveAsync(seg_buffer, CancellationToken.None);
                        ms.Write(seg_buffer.Array, seg_buffer.Offset, recv_result.Count);
                    }
                    while (!recv_result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (recv_result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            // do stuff
                        }
                    }
                }
            }
        }

        public async Task Stop() {
            await Task.Delay(0);
        }
    }
}