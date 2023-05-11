using System;
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

        public ConnectedDevice StreamDeck { get; set; }
        
        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] rxBuffer,
            IWebSocketReceiveResult rxResult)
        {
            string text = Encoding.GetString(rxBuffer);
            BizDeckJsonEvent evt = JsonConvert.DeserializeObject<BizDeckJsonEvent>(text);
            logger.Info($"OnMessageReceivedAsync: WebsockID[{context.Id}], Type[{evt.Type}], Data[{evt.Data}]");
            JObject data = (JObject)evt.Data;
            switch (evt.Type) { 
                case "del_button":
                    await HandleDeleteButtonDialogResult(context, (string)data);
                    break;
                case "add_button":
                    if (data.ContainsKey("name") && data.ContainsKey("json"))
                    {
                        await HandleAddButtonDialogResult(context, (string)data["name"], (string)data["json"]);
                    }
                    break;
            }
        }

        protected async Task HandleDeleteButtonDialogResult(IWebSocketContext ctx, string button_name)
        {
            // resume on any thread so we free this thread for more websock event handling
            (bool ok, string msg) = await config_helper.DeleteButton(button_name);
            if (!ok)
            { 
                logger.Error($"RaiseDeleteButtonDialog: del_button failed for name[{button_name}]");
                await SendNotification(ctx, "Delete button failed", msg);
            }
            else
            {
                // Update the Buttons tab on the GUI and StreamDeck
                await SendConfig(ctx);
                StreamDeck.ButtonList = config_helper.BizDeckConfig.ButtonMap;
            }
        }

        protected async Task HandleAddButtonDialogResult(IWebSocketContext ctx, string script_name, string script)
        {
            // resume on any thread so we free this thread for more websock event handling
            (bool ok, string msg) = await config_helper.AddButton(script_name, script);
            if (!ok)
            {
                logger.Error($"RaiseAddButtonDialog: add_button failed for name[{script_name}]");
                await SendNotification(ctx, "Add button failed", msg);
            }
            else
            {
                // Update the Buttons tab on the GUI and StreamDeck
                await SendConfig(ctx);
                StreamDeck.ButtonList = config_helper.BizDeckConfig.ButtonMap;
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
