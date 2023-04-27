using System;
using System.Threading.Tasks;

namespace BizDeck
{
    public class StartRecording : ButtonAction
    {
        private IRecorder recorder;
        private BizDeckLogger logger;

        public StartRecording(IRecorder r) {
            logger = new(this);
            recorder = r;
        }

        public override void Run()
        {

        }
        public async override Task RunAsync()
        {
            if (!recorder.HasBrowser())
            {
                logger.Info("Starting browser...");
                recorder.StartBrowser();
            }
            else
            {
                // TODO make the record button blink?
                logger.Info("Start recording...");
                await recorder.StartRecording().ConfigureAwait(false);
            }
        }
    }
}
