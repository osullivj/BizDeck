using System;
using System.Threading;
using EmbedIO.WebSockets;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    class BizDeckWebSockModule : WebSocketModule
    {
        ConfigHelper config_helper;
        public BizDeckWebSockModule(ConfigHelper ch) :
            base("/ws", true)
        {
            config_helper = ch;
            AddProtocol("json");
        }

        /// <inheritdoc />
        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] rxBuffer,
            IWebSocketReceiveResult rxResult)
        {
            string text = Encoding.GetString(rxBuffer);
            BizDeckJsonEvent evt = JsonUtils.DeserializeFromJson<BizDeckJsonEvent>(text);
            $"OnMessageReceivedAsync: Type:{evt.Type}, Data:{evt.Data}".Info();

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
            Logger.Info("client connected");
            await SendTargetedEvent(context, new BizDeckJsonEvent("connected")).ConfigureAwait(false);
            BizDeckJsonEvent config_event = new BizDeckJsonEvent("config");
            config_event.Data = this.config_helper;
            await SendTargetedEvent(context, config_event);

        }

        private Task SendTargetedEvent(IWebSocketContext context, BizDeckJsonEvent jsEvent)
        {
            return SendAsync(context, JsonUtils.SerializeToJson(jsEvent));
        }

        /// <inheritdoc />
        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            Logger.Info("client disconnected");
            return Task.CompletedTask;
        }

        public async Task BroadcastEvent(BizDeckJsonEvent jsEvent)
        {
            var json = JsonUtils.SerializeToJson(jsEvent);
            await BroadcastAsync(json).ConfigureAwait(false);
        }
    }
}
