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

        public ActionsButton(string name, BizDeckWebSockModule ws) {
            logger = new(this);
            config_helper = ConfigHelper.Instance;
            this.name = name;
            driver = new ActionsDriver(ws);
        }

        public override void Run() {
            logger.Info($"Run: {name}");
        }

        public async override Task<(bool, string)> RunAsync() {
            Run();
            bool ok = true;
            string result = null;
            JObject action_script = null;

            try {
                action_script = driver.LoadAndParseActionScript(name);
                (ok, result) = await driver.PlayActions(name, action_script).ConfigureAwait(false);
                logger.Info($"RunAsync: name[{name}], ok[{ok}], err[{result}]");
            }
            catch (Exception ex) {
                result = ex.Message;
                logger.Error($"RunAsync: name[{name}], result[{result}], {ex}");
                return (false, result);
            }
            return (ok, result);
        }
    }
}
