using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class StartRecording : ButtonAction
    {
        private IRecorder recorder;
        public StartRecording(IRecorder r) { recorder = r; }
        public override void Run()
        {
            
        }
        public async override Task RunAsync()
        {
            if (!recorder.HasBrowser())
            {
                $"Starting browser...".Info();
                recorder.StartBrowser();
            }
            else
            {
                // TODO make the record button blink?
                $"Start recording...".Info();
                await recorder.StartRecording();
            }
        }
    }
}
