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

        public async Task<BizDeckResult> PlayApp(string name_or_path) {
            AppLaunch launch;
            BizDeckResult load_app_result = await config_helper.LoadAppLaunch(name_or_path);
            if (!load_app_result.OK) {
                logger.Error($"PlayApp: {load_app_result}");
                if (websock != null) {
                    await websock.SendNotification(null, $"{name_or_path} app launch failed", load_app_result.Message);
                }
                return load_app_result;
            }
            launch = (AppLaunch)load_app_result.Payload;
            logger.Info($"PlayApp: {name_or_path}:{launch.ExeDocUrl}");
            // start default app, doc or url
            /*
            var process = new System.Diagnostics.Process() {
                StartInfo = new System.Diagnostics.ProcessStartInfo(launch.ExeDocUrl) {
                    // TODO: make these configgable in the app launch scripts
                    UseShellExecute = false,
                    ErrorDialog = true,
                    Arguments = launch.Args
                }
            }; */
            var process = new System.Diagnostics.Process();
            var start_info = new System.Diagnostics.ProcessStartInfo(launch.ExeDocUrl);
            if (launch.ExeDocUrl.StartsWith("http")) {
                // shell allows Windows to spot the URL and launch browser
                start_info.UseShellExecute = true;
            }
            else {
                start_info.UseShellExecute = false;
                start_info.ErrorDialog = true;
                start_info.Arguments = launch.Args;
            }
            process.StartInfo = start_info;
            // If this blocks with no visible error, check the path in your
            // steps json very carefully!
            process.Start();
            logger.Info($"PlayApp: running {name_or_path}:{launch}");
            return BizDeckResult.Success;
        }
    }
}
