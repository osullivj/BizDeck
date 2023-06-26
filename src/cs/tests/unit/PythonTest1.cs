using NUnit.Framework;
using BizDeck;
using CommandLine;

namespace BizDeckUnitTests {
    public class PythonTest1 {
        [SetUp]
        public void Setup() {
            string[] args = { "--config", TestContext.Parameters.Get("config") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
        }

        [Test]
        public void TestInit() {
            BizDeckResult result = BizDeckPython.Instance.Init(ConfigHelper.Instance);
            Assert.AreEqual(null, result.Payload);
            Assert.AreEqual(true, result.OK);

            Assert.Pass();
        }
    }
}