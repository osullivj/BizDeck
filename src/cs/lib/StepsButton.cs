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

        public StepsButton(string name) {
            logger = new(this);
            config_helper = ConfigHelper.Instance;
            this.name = name;
            driver = new PuppeteerDriver();
        }

        public override void Run() {
            logger.Info($"Run: {name}");
        }

        public async override Task<BizDeckResult> RunAsync() {
            string result = null;
            Run();
            BizDeckResult load_result = config_helper.LoadStepsOrActions(name);
            if (!load_result.OK) {
                return load_result;
            }
            JObject steps = JObject.Parse(result);
            BizDeckResult play_result = await driver.PlaySteps(name, steps).ConfigureAwait(false);
            logger.Info($"RunAsync: name[{name}], result[{result}]");
            return play_result;
        }
    }
}
