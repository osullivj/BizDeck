using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class StopRecording : ButtonAction
    {
        private IRecorder recorder;
        public StopRecording(IRecorder r) { recorder = r; }
        public override void Run()
        {
            
        }
        public async override Task RunAsync()
        {
            await recorder.Stop().ConfigureAwait(false);
        }
    }
}
