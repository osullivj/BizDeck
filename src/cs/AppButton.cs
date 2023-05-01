using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck
{
    public class AppButton:ButtonAction
    {
        AppLaunch app_launch = null;
        string name = null;
        BizDeckLogger logger;
        public AppButton(ConfigHelper ch, string name) {
            logger = new(this);
            this.name = name;
            app_launch = ch.LoadAppLaunch(name);
        }
        public override void Run() {
            logger.Info($"Run: {name}:{app_launch.ExeDocUrl}");
            // start default browser - or new tab - and point it at 
            // our web server
            var process = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(app_launch.ExeDocUrl) {
                    UseShellExecute = false, ErrorDialog = true, Arguments = app_launch.Args
                }
            };
            // If this blocks with no visible error, check the path in your
            // steps json very carefully!
            process.Start();
            logger.Info($"Run: running {name}:{app_launch.ExeDocUrl}");
        }

        public async override Task RunAsync()
        {
            Run();
            await Task.Delay(0).ConfigureAwait(false);
        }
    }
}
