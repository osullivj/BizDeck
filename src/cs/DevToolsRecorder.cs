using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Net.Http;
using System.Text;
// using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    public delegate void Del(string message);

    public class DevToolsRecorder : IRecorder
    {
        private ConfigHelper config_helper;
        private HttpClient edge_client;
        private Process browser;
        private string json_list_url;
        private string browser_cmd_line_args;

        private ClientWebSocket debug_websock;
        private CancellationTokenSource ws_recv_cancel_token_source;
        private byte[] debug_websock_buffer = new Byte[8192];
        private static int command_id = 101;
        private Dictionary<int, string> inflight_ws_request_cache = new();
        private Dictionary<string, string> bad_ws_request_cache = new();
        private Dictionary<string, string> good_ws_request_cache = new();
        private List<string> unmatched_response_cache = new();
        private List<string> bad_trace_response_cache = new();
        private List<string> good_trace_response_cache = new();
        private List<string> key_trace_response_cache = new();
        BizDeckLogger logger;

        public DevToolsRecorder(ConfigHelper ch)
        {
            logger = new(this);
            edge_client = new HttpClient();
            config_helper = ch;
            browser_cmd_line_args = $"--remote-debugging-port={config_helper.BizDeckConfig.BrowserRecorderPort} "
                                            + $" --user-data-dir={config_helper.GetFullLogPath()}";
            json_list_url = $"http://localhost:{config_helper.BizDeckConfig.BrowserRecorderPort}/json/list";
            browser = null;
        }

        public bool StartBrowser()
        {
            if (browser != null)
            {
                logger.Info($"StartBrowser: browser already running Id[{browser.Id}], Handle:[{browser.Handle}]");
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
            browser.StartInfo.FileName = config_helper.BizDeckConfig.BrowserPath;
            logger.Info($"StartBrowser: browser starting with path[{config_helper.BizDeckConfig.BrowserPath}], args:[{browser_cmd_line_args}]");
            browser.StartInfo.Arguments = browser_cmd_line_args;
            browser.Start();
            logger.Info($"StartBrowser: Id:[{browser.Id}], Handle:[{browser.Handle}]");
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
            var http_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.BrowserJsonListTimeout));
            debug_websock = new ClientWebSocket();
            var ws_connect_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.BrowserJsonListTimeout));
            try
            {
                var json_list_str = await edge_client.GetStringAsync(json_list_url,
                                                        http_cancel_token_source.Token).ConfigureAwait(false);
                logger.Info($"StartRecording: json/list:[{json_list_str}]");
                // Now we have a list from DevTools, so we deserialize,
                // and connect to each of the debug websocket URLs. 
                List<DevToolsJsonListResponse> json_list_arr = JsonConvert.DeserializeObject<List<DevToolsJsonListResponse>>(json_list_str);
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
                    logger.Error($"StartRecording: no candidate websock found");
                    // TODO: signal error to user, terminate recording session
                    return;
                }
                else
                {
                    string details = $"debuggerURL[{real_tab_response.WebSocketDebuggerUrl}] for browserURL[{real_tab_response.Url}]";
                    logger.Info($"StartRecording: connecting to {details}");
                }
                var uri = new System.Uri(real_tab_response.WebSocketDebuggerUrl);
                await debug_websock.ConnectAsync(uri, ws_connect_cancel_token_source.Token).ConfigureAwait(false);
                // We're connected, so clear websock request caches
                ClearWebsockRequestResponseCaches();
                // Send Tracing.start
                // https://chromedevtools.github.io/devtools-protocol/tot/Tracing/#method-start
                await SendRequest("Tracing.start", config_helper.TraceConfig).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) {
                logger.Error($"Recorder /json/list timeout:{ex.ToString()}");
                // TODO: add code here to redirect the browser to an
                // error page about msedge.exe instances.
            }
        }

        private void ClearWebsockRequestResponseCaches()
        {
            inflight_ws_request_cache.Clear();
            bad_ws_request_cache.Clear();
            good_ws_request_cache.Clear();
            unmatched_response_cache.Clear();
            bad_trace_response_cache.Clear();
        }

        public async Task SendRequest(string method, string parms=null)
        {
            // We're composing json with C# $ style in place formatting. So that means
            // we need to use escapes for special chars...
            // 1. Curly braces {} are used for in place vars so need to be escaped for
            //    pass through to Newtonsoft.JSON. We escape by doubling to {{ or }}
            //    at begin and end.
            // 2. Quotes must be escaped with back slash.
            // https://www.newtonsoft.com/json/help/html/SerializingCollections.htm
            string command_json = $"{{\"id\":{command_id},\"method\":\"{method}\"";
            if (parms != null)
            {
                command_json += $",\"params\":{parms}";
            }
            command_json += "}";
            logger.Info($"SendRequest: sending {command_json}");
            var encoded = Encoding.UTF8.GetBytes(command_json);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            var ws_send_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.BrowserWebsockTimeout));
            await debug_websock.SendAsync(buffer, WebSocketMessageType.Text, true, ws_send_cancel_token_source.Token).ConfigureAwait(false);
            // Now let's cache the request in antipation of a response
            inflight_ws_request_cache[command_id++] = command_json;
        }

        private bool OnResponse(string json_msg)
        {
            // JObject dev_tools_obj = JObject.Parse(json_msg);
            dynamic dobj = JObject.Parse(json_msg);
            // Is this a response to a specific request, or is it
            // trace data in response to a DevTools "Tracing.start" ?
            // Use JToken to check existence. Once we're sure of the
            // shape of the object we can address dobj members directly.
            JToken request_id_token = dobj["id"];
            JToken method_name_token = dobj["method"];
            if (request_id_token != null) {
                int request_id = dobj.id;
                // result object is empty, ergo no error field
                if (inflight_ws_request_cache.ContainsKey(request_id)) {
                    string matching_request = inflight_ws_request_cache[request_id];
                    inflight_ws_request_cache.Remove(request_id);
                    if (dobj.result.Count == null) // no error
                    {
                        good_ws_request_cache[matching_request] = json_msg;
                        logger.Info($"OnResponse: msg[{matching_request}] ack with resp[{json_msg}]");
                    }
                    else
                    {
                        bad_ws_request_cache[matching_request] = json_msg;
                        logger.Error($"OnResponse: msg[{matching_request}] nack with resp[{json_msg}]");
                    }
                }
            }
            // there's no id field at top level of message
            else if (method_name_token != null) {
                string method = dobj.method;
                if (method == "Tracing.tracingComplete")
                {
                    // End of trace data, so we can drop the socket connection.
                    return false;
                }
                // "params" is a C# keyword, so we cannot ref dobj.params
                // The field itself should be an array
                JObject params_obj = dobj["params"] as JObject;
                JArray value_array = params_obj["value"] as JArray;
                if (value_array == null)
                {
                    logger.Error($"OnResponse: method[{method}] params not JArray msg[{json_msg}]");
                    bad_trace_response_cache.Add(json_msg);
                }
                else
                {
                    foreach( JToken jtok in value_array) {
                        // __metadata gets sent even if in traceConfig.excludedCategories
                        JObject parm = jtok as JObject;
                        string cat = (string)parm["cat"];
                        if (cat != "__metadata")
                        {
                            logger.Info($"OnResponse: method[{method}] parm[{parm}]");
                        }
                    }
                    good_trace_response_cache.Add(json_msg);
                }
                logger.Info($"OnResponse: method[{method}] with parms[{dobj["params"]}]");
            }
            return true;
        }

        private async Task ReceiveAsync()
        {
            // TODO: how to cancel or close the streams on stop recording
            // or ctrl-C or error
            ws_recv_cancel_token_source = new();
            WebSocketReceiveResult recv_result = null;
            ArraySegment<Byte> seg_buffer = new ArraySegment<byte>(debug_websock_buffer);
            while (debug_websock.State == WebSocketState.Open)
            {
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        recv_result = await debug_websock.ReceiveAsync(seg_buffer, ws_recv_cancel_token_source.Token);
                        ms.Write(seg_buffer.Array, seg_buffer.Offset, recv_result.Count);
                    }
                    while (!recv_result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (recv_result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            var msg = reader.ReadToEnd();
                            logger.Info($"ReceiveAsync: msg[{msg}]");
                            bool keep_websock_open = OnResponse(msg);
                            if (!keep_websock_open)
                            {

                                var ws_close_cancel_token_source = new CancellationTokenSource(TimeSpan.FromSeconds(config_helper.BizDeckConfig.BrowserJsonListTimeout));
                                await debug_websock.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                    "Tracing.tracingComplete recved", ws_close_cancel_token_source.Token);
                                logger.Info($"ReceiveAsync: websock closed");
                            }
                        }
                    }
                }
            }
        }

        public async Task Stop() {
            await SendRequest("Tracing.end");
            // Now recv the trace results...
            await ReceiveAsync();
            // TODO add code to print cache states
            // Close the browser instance; if we leave it running it will
            // hog the websock listener port.
            browser.Kill();
        }
    }
}