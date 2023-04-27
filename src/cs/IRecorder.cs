using System;
using System.Diagnostics;
using System.Threading.Tasks;

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