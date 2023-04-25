using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class NullOpButton:ButtonAction
    {
        public NullOpButton() { }
        public override void Run() {
            $"NullOpButton".Info();
        }

        public async override Task RunAsync()
        {
            Run();
            await Task.Delay(0).ConfigureAwait(false);
        }
    }
}
