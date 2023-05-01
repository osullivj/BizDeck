using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck
{
    public class StepsButton:ButtonAction
    {
        string steps;
        string name;
        BizDeckLogger logger;
        PuppeteerDriver driver;

        public StepsButton(ConfigHelper ch, string name) {
            logger = new(this);
            this.name = name;
            steps = ch.LoadSteps(name);
            driver = new PuppeteerDriver(ch);
        }
        public override void Run() {
            logger.Info($"Run: {name}:{steps}");
        }

        public async override Task RunAsync()
        {
            Run();
            await driver.PlaySteps(steps).ConfigureAwait(false);
        }
    }
}
