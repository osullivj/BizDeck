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
        dynamic steps;
        string name;
        BizDeckLogger logger;
        PuppeteerDriver driver;

        public StepsButton(ConfigHelper ch, string name) {
            logger = new(this);
            this.name = name;
            steps = JObject.Parse(ch.LoadStepsOrActions(name));
            driver = new PuppeteerDriver(ch);
        }

        public override void Run() {
            logger.Info($"Run: {name}:{steps}");
        }

        public async override Task<(bool, string)> RunAsync() {
            Run();
            bool ok = await driver.PlaySteps(name, steps).ConfigureAwait(false);
            logger.Info($"RunAsync: name[{name}], ok[{ok}]");
            return (true, null);
        }
    }
}
