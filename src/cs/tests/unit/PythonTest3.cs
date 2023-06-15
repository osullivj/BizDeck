using System.Threading.Tasks;
using NUnit.Framework;
using BizDeck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeckUnitTests {
    public class PythonTest3 {
        [SetUp]
        public void Setup() {
            string[] args = { "--appdata", TestContext.Parameters.Get("appdata", "c:\\osullivj\\src") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
            // ConfigHelper and ActionsDriver will log post init,
            // so create logging subsys
            BizDeckLogger.InitLogging();
            // pytest1.py writes to console
            Win32.AllocConsole();
            (bool ok, string err) = BizDeckPython.Instance.Init(ConfigHelper.Instance);
        }

        [Test]
        public async Task TestLoadQuandlYieldAction() {
            // Load up cg/pytest1.json
            string actions_name = "load_quandl_yield";
            bool ok = false;
            string result = null;
            (ok, result) = ConfigHelper.Instance.LoadStepsOrActions(actions_name);
            Assert.AreNotEqual(null, result);
            Assert.AreEqual(true, ok);
            ActionsDriver actions_driver = new();
            JObject actions = JObject.Parse(result);
            (ok, result) = await actions_driver.PlayActions(actions_name, actions).ConfigureAwait(false);
            Assert.AreEqual(null, result);
            Assert.AreEqual(true, ok);
            Assert.Pass();
        }
    }
}