﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {

    // delegates for methods that update websock clients inc the browser GUI
    // this enables eg AppDriver, PuppeteerDriver  ActionsDriver to update
    // GUIs with
    public delegate Task NotifyGUI(IWebSocketContext context, string title, string body, bool fade = false);
    public delegate Task BroadcastJson(string json);

    public class BizDeckWebSockModule : WebSocketModule {
        ConfigHelper config_helper;
        BizDeckStatus status;
        BizDeckLogger logger;
        List<string> add_button_request_keys = new() { "name", "json", "background" };

        public BizDeckWebSockModule(ConfigHelper ch)
            :base("/ws", true)
        {
            logger = new BizDeckLogger(this);
            config_helper = ch;
            status = BizDeckStatus.Instance;
            AddProtocol("json");
        }

        public ConnectedDeck StreamDeck { get; set; }
        public Server MainServerObject { get; set; }
        
        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] rxBuffer,
            IWebSocketReceiveResult rxResult)
        {
            string text = Encoding.GetString(rxBuffer);
            BizDeckJsonEvent evt = JsonConvert.DeserializeObject<BizDeckJsonEvent>(text);
            logger.Info($"OnMessageReceivedAsync: WebsockID[{context.Id}], Type[{evt.Type}], Data[{evt.Data}]");
            switch (evt.Type) { 
                case "del_button":
                    await HandleDeleteButtonDialogResult(context, (string)evt.Data);
                    break;
                case "add_button":
                    await HandleAddButtonDialogResult(context, evt.Data);
                    break;
                case "set_brightness":
                    // When we put an int directly into data like so...
                    // {Type:"set_brightness",Data:10} NewtonsoftJson
                    // marshalls into System.Int64 as Data is defined as
                    // being an object, so base types cannot be marshalling
                    // targets, hence the use of Convert as applying
                    // an (int) case to System.Int64 throws an exception
                    MainServerObject.SetDeckBrightness((int)Convert.ToInt32(evt.Data));
                    break;
                case "save_brightness":
                    config_helper.BizDeckConfig.DeckBrightnessPercentage = (int)Convert.ToInt32(evt.Data);
                    await config_helper.SaveConfig();
                    break;
            }
        }

        protected async Task HandleDeleteButtonDialogResult(IWebSocketContext ctx, string button_name) {
            // resume on any thread so we free this thread for more websock event handling
            BizDeckResult delete_result = await config_helper.DeleteButton(button_name);
            if (!delete_result.OK) { 
                logger.Error($"HandleDeleteButtonDialogResult: del_button failed for name[{button_name}]");
                await SendNotification(ctx, "Delete button failed", delete_result.Message);
            }
            else {
                // Update the Buttons tab on the GUI and the StreamDeck
                await SendConfig(ctx);
                BizDeckResult rebuild_result = MainServerObject.RebuildButtonMaps();
                if (!rebuild_result.OK) {
                    await SendNotification(ctx, "Delete button failed on RebuildButtonMaps", rebuild_result.Message);
                }
            }
        }

        protected async Task HandleAddButtonDialogResult(IWebSocketContext ctx, object evt_Data) {
            string script_name = null;
            string script = null;
            string background = null;
            if (evt_Data is JObject) {
                JObject data = (JObject)evt_Data;
                if (add_button_request_keys.TrueForAll(s => data.ContainsKey(s))) {
                    script_name = (string)data["name"];
                    script = (string)data["json"];
                    background = (string)data["background"];
                }
            }
            if (script_name == null || script == null) {
                logger.Error($"HandleAddButtonDialogResult: cannot marshal script data from {evt_Data}");
            }
            // resume on any thread so we free this thread for more websock event handling
            BizDeckResult add_button_result = await config_helper.AddButton(script_name, script, background);
            if (!add_button_result.OK) {
                logger.Error($"HandleAddButtonDialogResult: add_button failed for name[{script_name}]");
                await SendNotification(ctx, "Add button failed", add_button_result.Message);
            }
            else {
                // Update the Buttons tab on the GUI and the StreamDeck. 
                await SendConfig(ctx);
                BizDeckResult rebuild_result = MainServerObject.RebuildButtonMaps();
                if (!rebuild_result.OK) {
                    await SendNotification(ctx, "Add button failed on RebuildButtonMaps", rebuild_result.Message);
                }
            }
        }


        protected override async Task OnClientConnectedAsync(IWebSocketContext context)
        {
            logger.Info($"OnClientConnectedAsync: WebsockID[{context.Id}]");
            // Let the client know we've accepted the connection. Not strictly necessary,
            // but very useful to see the incoming connected msg when debugging on the GUI side.
            await SendTargetedEvent(context, new BizDeckJsonEvent("connected")).ConfigureAwait(false);
            // Populate GUI tabs: config won't change during process lifetime, but status and
            // cache state will change, and a newly connected GUI needs to latest state for both.
            await SendStatus(context);
            await SendConfig(context);
            await SendCache(context);
        }

        protected async Task SendConfig(IWebSocketContext context)
        {
            logger.Info($"SendConfig: WebsockID[{context.Id}]");
            BizDeckJsonEvent config_event = new BizDeckJsonEvent("config");
            config_event.Data = this.config_helper;
            await SendTargetedEvent(context, config_event).ConfigureAwait(false);
        }

        protected async Task SendStatus(IWebSocketContext context) {
            logger.Info($"SendStatus: WebsockID[{context.Id}]");
            BizDeckJsonEvent status_event = new BizDeckJsonEvent("status");
            status_event.Data = this.status;
            await SendTargetedEvent(context, status_event).ConfigureAwait(false);
        }

        protected async Task SendCache(IWebSocketContext context) {
            logger.Info($"SendCache: WebsockID[{context.Id}]");
            // SerializeJsonEvent(false) so we don't reset HasChanged and
            // trigger an unnecessary GUI update.
            string cache_event_json = DataCache.Instance.SerializeToJsonEvent(false);
            await SendTargetedEvent(context, cache_event_json).ConfigureAwait(false);
        }

        public async Task SendNotification(IWebSocketContext context, string title, string body, 
                                                                            bool fade=false)
        {
            string context_id = "broadcast";
            if (context != null) {
                context_id = context.Id;
            }
            logger.Info($"SendNotification: WebsockID[{context_id}] title[{title}] body[{body}]");
            BizDeckJsonEvent notification_event = new BizDeckJsonEvent("notification");
            JObject data = new();
            data.Add("title", title);
            data.Add("body", body);
            if (!fade) data.Add("expiryMs", -1);
            notification_event.Data = data;
            if (context != null) {
                await SendTargetedEvent(context, notification_event).ConfigureAwait(false);
            }
            else {
                await BroadcastEvent(notification_event).ConfigureAwait(false);
            }
        }

        private Task SendTargetedEvent(IWebSocketContext context, BizDeckJsonEvent json_event)
        {
            return SendAsync(context, JsonConvert.SerializeObject(json_event));
        }

        private Task SendTargetedEvent(IWebSocketContext context, string json) {
            return SendAsync(context, json);
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            logger.Info($"OnClientDisconnectedAsync: WebsockID[{context.Id}], Local[{context.IsLocal}], Remote[{context.RemoteEndPoint.Address}]");
            return Task.CompletedTask;
        }

        public async Task BroadcastEvent(BizDeckJsonEvent jsEvent)
        {
            var json = JsonConvert.SerializeObject(jsEvent);
            await BroadcastAsync(json).ConfigureAwait(false);
        }

        public async Task BroadcastJson(string json) {
            await BroadcastAsync(json).ConfigureAwait(false);
        }
    }
}
