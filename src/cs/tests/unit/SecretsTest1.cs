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
            string[] args = { "--appdata", TestContext.Parameters.Get("appdata", "c:\\osullivj\\src") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
        }

        [Test]
        public void TestPlayScriptSecrets() {
            var bdconfig = ConfigHelper.Instance.BizDeckConfig;
            // TODO add code that loads tiingo_gui_login and validates
            // subs against test secrets cfg
            Assert.Pass();
        }

        [Test]
        public void TestHTTPSecrets() {
            var bdconfig = ConfigHelper.Instance.BizDeckConfig;
            // TODO add code that loads quandl_rates and checks that
            // the URLs are decorated with auth keys correctly
            Assert.Pass();
        }
    }
}