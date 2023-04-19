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
    public interface IRecorder
    {
        public Task StartBrowser();

        public Task Start();

        public Task Stop();
    }
}