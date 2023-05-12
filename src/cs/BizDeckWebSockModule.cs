﻿using System;
using System.Threading.Tasks;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    public class BizDeckWebSockModule : WebSocketModule
    {
        ConfigHelper config_helper;
        BizDeckLogger logger;
        public BizDeckWebSockModule(ConfigHelper ch) :
            base("/ws", true)
        {
            logger = new BizDeckLogger(this);
            config_helper = ch;
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
            }
        }

        protected async Task HandleDeleteButtonDialogResult(IWebSocketContext ctx, string button_name)
        {
            // resume on any thread so we free this thread for more websock event handling
            (bool ok, string msg) = await config_helper.DeleteButton(button_name);
            if (!ok)
            { 
                logger.Error($"HandleDeleteButtonDialogResult: del_button failed for name[{button_name}]");
                await SendNotification(ctx, "Delete button failed", msg);
            }
            else
            {
                // Update the Buttons tab on the GUI and StreamDeck
                await SendConfig(ctx);
                StreamDeck.ButtonDefnList = config_helper.BizDeckConfig.ButtonList;
                MainServerObject.RebuildButtonActionMap();
            }
        }

        protected async Task HandleAddButtonDialogResult(IWebSocketContext ctx, object evt_Data)
        {
            string script_name = null;
            string script = null;
            if (evt_Data is JObject) {
                JObject data = (JObject)evt_Data;
                if (data.ContainsKey("name") && data.ContainsKey("json")) {
                    script_name = (string)data["name"];
                    script = (string)data["json"];
                }
            }
            if (script_name == null || script == null) {
                logger.Error($"HandleAddButtonDialogResult: cannot marshal script data from {evt_Data}");
            }
            // resume on any thread so we free this thread for more websock event handling
            (bool ok, string msg) = await config_helper.AddButton(script_name, script);
            if (!ok)
            {
                logger.Error($"HandleAddButtonDialogResult: add_button failed for name[{script_name}]");
                await SendNotification(ctx, "Add button failed", msg);
            }
            else
            {
                // Update the Buttons tab on the GUI and StreamDeck
                await SendConfig(ctx);
                StreamDeck.ButtonDefnList = config_helper.BizDeckConfig.ButtonList;
                MainServerObject.RebuildButtonActionMap();
            }
        }

        protected override async Task OnClientConnectedAsync(IWebSocketContext context)
        {
            logger.Info($"OnClientConnectedAsync: WebsockID[{context.Id}]");
            await SendTargetedEvent(context, new BizDeckJsonEvent("connected")).ConfigureAwait(false);
            await SendConfig(context);
        }

        protected async Task SendConfig(IWebSocketContext context)
        {
            logger.Info($"SendConfig: WebsockID[{context.Id}]");
            BizDeckJsonEvent config_event = new BizDeckJsonEvent("config");
            config_event.Data = this.config_helper;
            await SendTargetedEvent(context, config_event).ConfigureAwait(false);
        }

        public async Task SendNotification(IWebSocketContext context, string title, string body, 
                                                                            bool fade=false)
        {
            logger.Info($"SendNotification: WebsockID[{context.Id}] title[{title}] body[{body}]");
            BizDeckJsonEvent notification_event = new BizDeckJsonEvent("notification");
            JObject data = new();
            data.Add("title", title);
            data.Add("body", body);
            if (!fade) data.Add("expiryMs", -1);
            notification_event.Data = data;
            if (context != null)
            {
                await SendTargetedEvent(context, notification_event).ConfigureAwait(false);
            }
            else
            {
                await BroadcastEvent(notification_event).ConfigureAwait(false);
            }
        }

        private Task SendTargetedEvent(IWebSocketContext context, BizDeckJsonEvent jsEvent)
        {
            return SendAsync(context, JsonConvert.SerializeObject(jsEvent));
        }

        /// <inheritdoc />
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
    }
}
