using System.Threading.Tasks;

namespace BizDeck
{
    public class AppDriver {
        ConfigHelper config_helper;
        BizDeckLogger logger;
        BizDeckWebSockModule websock;

        // ButtonAction ctor: any connected GUIs will get notification on fail
        // NB button actions do not have an HttpContext, so need the ws to
        // talk back to the GUI.
        public AppDriver(BizDeckWebSockModule ws) {
            logger = new(this);
            config_helper = ConfigHelper.Instance;
            websock = ws;
        }

        // API ctor: the caller has an HTTP context that can be used for
        // error reporting.
        public AppDriver() {
            logger = new(this);
            config_helper = ConfigHelper.Instance;
            websock = null;
        }

        public async Task<(bool, string)> PlayApp(string name_or_path) {
            bool ok = true;
            string error = null;
            AppLaunch launch;
            (ok, launch, error) = await config_helper.LoadAppLaunch(name_or_path);
            if (!ok) {
                logger.Error($"PlayApp: {error}");
                if (websock != null) {
                    await websock.SendNotification(null, $"{name_or_path} app launch failed", error);
                }
                return (ok, error);
            }
            logger.Info($"Run: {name_or_path}:{launch.ExeDocUrl}");
            // start default app, doc or url
            var process = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(launch.ExeDocUrl) {
                    // TODO: make these configgable in the app launch scripts
                    UseShellExecute = false,
                    ErrorDialog = true,
                    Arguments = launch.Args
                }
            };
            // If this blocks with no visible error, check the path in your
            // steps json very carefully!
            process.Start();
            logger.Info($"Run: running {name_or_path}:{launch}");
            return (true, null);
        }
    }
}
