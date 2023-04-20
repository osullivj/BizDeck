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
        public bool StartBrowser();
        public bool HasBrowser();
        public Task StartRecording();
        public Task Stop();
    }
}