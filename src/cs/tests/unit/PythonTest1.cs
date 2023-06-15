using NUnit.Framework;
using BizDeck;
using CommandLine;

namespace BizDeckUnitTests {
    public class PythonTest1 {
        [SetUp]
        public void Setup() {
            string[] args = { "--appdata", TestContext.Parameters.Get("appdata", "c:\\osullivj\\src") };
            CmdLineOptions.InitAndLoadConfigHelper(args);
        }

        [Test]
        public void TestInit() {
            (bool ok, string err) = BizDeckPython.Instance.Init(ConfigHelper.Instance);
            Assert.AreEqual(null, err);
            Assert.AreEqual(true, ok);

            Assert.Pass();
        }
    }
}