using System;
using System.Threading.Tasks;
using EmbedIO.WebSockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    class BizDeckWebSockModule : WebSocketModule
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

        /// <inheritdoc />
        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] rxBuffer,
            IWebSocketReceiveResult rxResult)
        {
            string text = Encoding.GetString(rxBuffer);
            BizDeckJsonEvent evt = JsonConvert.DeserializeObject<BizDeckJsonEvent>(text);
            logger.Info($"OnMessageReceivedAsync: WebsockID[{context.Id}], Type[{evt.Type}], Data[{evt.Data}]");
            switch (evt.Type)
            {
                case "del_button":
                    await RaiseDeleteButtonDialog(context, (string)evt.Data);
                    break;
                case "add_button":
                    await RaiseAddButtonDialog(context);
                    break;
            }
        }

        protected async Task RaiseDeleteButtonDialog(IWebSocketContext ctx, string button_name)
        {
            // resume on any thread so we free this thread for more websock event handling
            bool ok = await config_helper.DeleteButton(button_name).ConfigureAwait(false);
            if (!ok)
            {
                logger.Error($"RaiseDeleteButtonDialog: del_button failed for name[{button_name}]");
            }
            else
            {
                // Update the Buttons tab on the GUI
                await SendConfig(ctx);
            }
        }

        protected async Task RaiseAddButtonDialog(IWebSocketContext ctx)
        {
            // resume on any thread so we free this thread for more websock event handling
            bool ok = await config_helper.AddButton("").ConfigureAwait(false);
            if (!ok)
            {
                logger.Error($"RaiseAddButtonDialog: add_button failed for name[]");
            }
            else
            {
                // Update the Buttons tab on the GUI
                await SendConfig(ctx);
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
