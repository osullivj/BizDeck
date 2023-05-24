using System.Threading.Tasks;

namespace BizDeck
{
    public class AppButton:ButtonAction
    {
        ConfigHelper config_helper;
        string name = null;
        BizDeckLogger logger;
        AppDriver app_driver;

        public AppButton(ConfigHelper ch, string name, BizDeckWebSockModule ws) {
            logger = new(this);
            app_driver = new(ch, ws);
            this.name = name;
            config_helper = ch;
        }

        public override void Run() { }

        public async override Task<(bool, string)> RunAsync() {
            return await app_driver.PlayApp(name);
        }

 
    }
}
