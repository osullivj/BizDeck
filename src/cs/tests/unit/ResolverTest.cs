using NUnit.Framework;
using BizDeck;


namespace BizDeckUnitTests {
    public class ResolverTest {
        private string cargo_id = "CARG-21556";

        [SetUp]
        public void Setup() {
            NameStack.Instance.AddNameValue("cargo_id", cargo_id);
        }

        [Test]
        public void TestAriaSelectorInterpolation() {
            string aria_selector = "aria/<cargo_id>[role=\"button\"]";
            BizDeckResult result = NameStack.Instance.Interpolate(aria_selector);
            Assert.AreEqual(result.OK, true);
            Assert.AreEqual(result.Payload, "aria/CARG-21556[role=\"button\"]");
        }

        [Test]
        public void TestTextSelectorInterpolation() {
            string text_selector = "text/<cargo_id>";
            BizDeckResult result = NameStack.Instance.Interpolate(text_selector);
            Assert.AreEqual(result.OK, true);
            Assert.AreEqual(result.Payload, "text/CARG-21556");
        }
    }
}