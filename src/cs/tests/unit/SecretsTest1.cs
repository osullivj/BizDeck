using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using BizDeck;

namespace BizDeckUnitTests {
    public class SecretsTest1 {
        [SetUp]
        public void Setup() {
            // TODO: need and alternate cfg dir for test config
            // probably need alt dir tree for test scripts too
            // two secrets tests: HTTP construction, secret sub in tiingo_gui_login
            // Q: should we make ConfigDir or SecretsPath overrideable so tests
            // can set here to pick up alternates? JOS 2023-06-22
            string[] args = { "--config", TestContext.Parameters.Get("config") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
        }

        [Test]
        public void TestPlayScriptSecrets() {
            var bdconfig = ConfigHelper.Instance.BizDeckConfig;
            var token_result = NameStack.Instance.Resolve("secrets.quandl.auth_token");
            Assert.AreEqual("unit_test_token", token_result.Message);
            Assert.Pass();
        }

        [Test]
        public void TestHTTPSecrets() {
            var http_spec_map = ConfigHelper.Instance.HttpFormatMap["quandl"];
            var bd_http_format = http_spec_map["url"];
            var actions_driver = new ActionsDriver();
            // See ActionsDriver.PlayScript()
            dynamic action_script = actions_driver.LoadAndParseActionScript("quandl_rates");
            JArray actions = action_script.actions;
            var result = actions_driver.ExpandHttpFormat(bd_http_format, (JObject)actions[0]);
            Assert.AreEqual(true, result.OK);
            Assert.AreEqual("https://www.quandl.com/api/v1/datasets/FRED/DED3.csv?auth_token=unit_test_token",
                result.Message);
            Assert.Pass();
        }
    }
}