using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    public class StepsButton:ButtonAction
    {
        string name;
        BizDeckLogger logger;
        PuppeteerDriver driver;
        ConfigHelper config_helper;

        public StepsButton(ConfigHelper ch, string name) {
            logger = new(this);
            config_helper = ch;
            this.name = name;
            driver = new PuppeteerDriver(ch);
        }

        public override void Run() {
            logger.Info($"Run: {name}");
        }

        public async override Task<(bool, string)> RunAsync() {
            bool ok = true;
            string result = null;
            Run();
            (ok, result) = config_helper.LoadStepsOrActions(name);
            if (!ok) {
                return (ok, result);
            }
            JObject steps = JObject.Parse(result);
            (ok, result) = await driver.PlaySteps(name, steps).ConfigureAwait(false);
            logger.Info($"RunAsync: name[{name}], ok[{ok}]");
            return (ok, result);
        }
    }
}
