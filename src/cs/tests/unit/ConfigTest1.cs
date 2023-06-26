using NUnit.Framework;
using BizDeck;
using CommandLine;

namespace BizDeckUnitTests {
    public class ConfigTest1 {
        [SetUp]
        public void Setup() {
            string[] args = { "--config", TestContext.Parameters.Get("config") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
        }

        [Test]
        public void TestConfigLoading() {
            var bdconfig = ConfigHelper.Instance.BizDeckConfig;
            Assert.AreEqual(bdconfig.BackgroundDefault, "bg3");
            Assert.AreEqual(bdconfig.HTTPHostName, "localhost");
            Assert.Pass();
        }
    }
}