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
        ConfigHelper config_helper;
        string name = null;
        BizDeckLogger logger;
        BizDeckWebSockModule websock;

        public AppButton(ConfigHelper ch, string name, BizDeckWebSockModule ws) {
            logger = new(this);
            this.name = name;
            config_helper = ch;
            websock = ws;
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
            if (app_launch == null)
            {
                (bool ok, AppLaunch launch, string error) = await config_helper.LoadAppLaunch(name);
                if (!ok)
                {
                    logger.Error($"RunAsync: {error}");
                    await websock.SendNotification(null, $"{name} app launch failed", error);
                    return;
                }
                app_launch = launch;
            }
            if (app_launch != null) Run();
        }
    }
}
