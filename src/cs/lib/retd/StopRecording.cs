using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck
{
    public class StopRecording : ButtonAction
    {
        private IRecorder recorder;
        public StopRecording(IRecorder r) { recorder = r; }
        public override void Run()
        {
            
        }
        public async override Task<(bool, string)> RunAsync()
        {
            await recorder.Stop().ConfigureAwait(false);
            return (true, null);
        }
    }
}
