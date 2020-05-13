using NUnit.Framework;

namespace Tests {

    public class TestUtils {

        [Test]
        public void TestFirstCharToLower() {
            Assert.AreEqual("best",Utils.LowerFirstChar("Best"));
        }

        [Test]
        public void TestFirstCharToUpper() {
            Assert.AreEqual("lul",Utils.LowerFirstChar("Lul"));
        }

    }

}
