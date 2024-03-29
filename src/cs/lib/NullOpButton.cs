﻿using System.Threading.Tasks;

namespace BizDeck
{
    public class NullOpButton:ButtonAction
    {
        private BizDeckLogger logger;
        public NullOpButton() {
            logger = new(this);
        }

        public override void Run() {
            logger.Info("Run");
        }

        public async override Task<BizDeckResult> RunAsync()
        {
            Run();
            await Task.Delay(0).ConfigureAwait(false);
            return BizDeckResult.Success;
        }
    }
}
