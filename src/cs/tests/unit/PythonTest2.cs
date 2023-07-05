using System.Threading.Tasks;
using NUnit.Framework;
using BizDeck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeckUnitTests {
    public class PythonTest2 {
        [SetUp]
        public void Setup() {
            string[] args = { "--config", TestContext.Parameters.Get("config") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
            // ConfigHelper and ActionsDriver will log post init,
            // so create logging subsys
            BizDeckLogger.InitLogging("python_test2");
            // pytest1.py writes to console
            Win32.AllocConsole();
            BizDeckResult py_init_result = BizDeckPython.Instance.Init(ConfigHelper.Instance);
        }

        [Test]
        public async Task TestPytest1Action() {
            // Load up scripts/actions/utest_python_batch.json
            string actions_name = "utest_python_batch";
            BizDeckResult load_result = ConfigHelper.Instance.LoadStepsOrActions(actions_name);
            Assert.AreNotEqual(null, load_result.Payload);
            Assert.AreEqual(true, load_result.OK);
            ActionsDriver actions_driver = new();
            JObject actions = JObject.Parse(load_result.Message);
            BizDeckResult play_result = await actions_driver.PlayActions(actions_name, actions).ConfigureAwait(false);
            Assert.AreEqual(null, play_result.Payload);
            Assert.AreEqual(true, play_result.OK);
            Assert.Pass();
        }
    }
}