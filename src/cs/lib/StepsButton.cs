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
        ConfigHelper config_helper;

        public StepsButton(string name) {
            logger = new(this);
            config_helper = ConfigHelper.Instance;
            this.name = name;
        }

        public override void Run() {
            logger.Info($"Run: {name}");
        }

        public async override Task<BizDeckResult> RunAsync() {
            try {
                Run();
                BizDeckResult load_result = config_helper.LoadStepsOrActions(name);
                if (!load_result.OK) {
                    return load_result;
                }
                JObject steps = JObject.Parse(load_result.Message);
                BizDeckResult play_result = await PuppeteerDriver.Instance.PlaySteps(name, steps).ConfigureAwait(false);
                logger.Info($"RunAsync: name[{name}], result[{play_result}]");
                return play_result;
            }
            catch (Exception ex) {
                logger.Error($"RunAsync: name[{name}], {ex}");
                return new BizDeckResult(ex.Message);
            }
        }
    }
}
