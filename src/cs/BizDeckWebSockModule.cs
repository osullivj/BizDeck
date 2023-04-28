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

            if (evt.Type == "spam")
            {
                // wait some time, simulating actual work being done and then respond with a big chunk of text
                Random rnd = new Random();
                await Task.Delay(rnd.Next(50, 150));
                var responseEvent = new BizDeckJsonEvent("spam-back")
                {
                    Data = JsDataRow.GenerateLargeTable()
                };
                await SendTargetedEvent(context, responseEvent).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        protected override async Task OnClientConnectedAsync(IWebSocketContext context)
        {
            logger.Info($"OnClientConnectedAsync: WebsockID[{context.Id}]");
            await SendTargetedEvent(context, new BizDeckJsonEvent("connected")).ConfigureAwait(false);
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
