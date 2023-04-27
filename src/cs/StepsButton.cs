using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck
{
    public class StepsButton:ButtonAction
    {
        BizDeckSteps steps = null;
        string name = null;
        BizDeckLogger logger;
        public StepsButton(ConfigHelper ch, string name) {
            logger = new(this);
            this.name = name;
            steps = ch.LoadSteps(name);
        }
        public override void Run() {
            logger.Info($"Run: {name}:{steps.ExeDocUrl}");
            // start default browser - or new tab - and point it at 
            // our web server
            var process = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(steps.ExeDocUrl) {
                    UseShellExecute = false, ErrorDialog = true, Arguments = steps.Args
                }
            };
            // If this blocks with no visible error, check the path in your
            // steps json very carefully!
            process.Start();
            logger.Info($"Run: running {name}:{steps.ExeDocUrl}");
        }

        public async override Task RunAsync()
        {
            Run();
            await Task.Delay(0).ConfigureAwait(false);
        }
    }
}
