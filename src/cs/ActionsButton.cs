using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    public class ActionsButton:ButtonAction {
        string name;
        BizDeckLogger logger;
        ActionsDriver driver;
        ConfigHelper config_helper;

        public ActionsButton(ConfigHelper ch, string name, BizDeckPython py) {
            logger = new(this);
            config_helper = ch;
            this.name = name;
            driver = new ActionsDriver(ch, py);
        }

        public override void Run() {
            logger.Info($"Run: {name}");
        }

        public async override Task<(bool, string)> RunAsync() {
            bool ok = true;
            string result = null;
            JObject action_script = null;
            Run();
            (ok, result) = config_helper.LoadStepsOrActions(name);
            if (!ok) {
                return (ok, result);
            }
            try {
                action_script = JObject.Parse(result);
            }
            catch (JsonReaderException ex) {
                result = $"JSON error reading {name}, {ex}";
                logger.Error($"RunAsync: {result}");
                return (false, result);
            }
            (ok, result) = await driver.PlayActions(name, action_script).ConfigureAwait(false);
            logger.Info($"RunAsync: name[{name}], ok[{ok}], err[{result}]");
            return (ok, result);
        }
    }
}
