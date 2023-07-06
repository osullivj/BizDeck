using System.Threading.Tasks;

namespace BizDeck
{
    public class AppButton:ButtonAction
    {
        string name = null;
        BizDeckLogger logger;
        AppDriver app_driver;

        public AppButton(string name, BizDeckWebSockModule ws) {
            logger = new(this);
            app_driver = new(ws);
            this.name = name;
        }

        public override void Run() { }

        public async override Task<BizDeckResult> RunAsync() {
            logger.Info($"RunAsync: {name}");
            return await app_driver.PlayApp(name);
        }

 
    }
}
