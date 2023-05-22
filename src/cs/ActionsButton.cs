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
        JObject action_script;
        string name;
        BizDeckLogger logger;
        ActionsDriver driver;

        public ActionsButton(ConfigHelper ch, string name) {
            logger = new(this);
            this.name = name;
            action_script = JObject.Parse(ch.LoadStepsOrActions(name));
            driver = new ActionsDriver(ch);
        }

        public override void Run() {
            logger.Info($"Run: {name}:{action_script}");
        }

        public async override Task<(bool, string)> RunAsync() {
            Run();
            (bool ok, string error) = await driver.PlayActions(name, action_script).ConfigureAwait(false);
            logger.Info($"RunAsync: name[{name}], ok[{ok}], err[{error}]");
            return (ok, error);
        }
    }
}
