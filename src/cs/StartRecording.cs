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
        private Recorder recorder;
        public StartRecording(Recorder r) { recorder = r; }
        public override void Run()
        {
            
        }
        public async override Task RunAsync()
        {
            $"StartRecording awaiting...".Info();
            await recorder.Start();
        }
    }
}
