using NUnit.Framework;

namespace Tests {

    public class TestRewriter {






        [SetUp]
        public void Setup() {
        }

        // [TestFixture]
        // public void BestTestFixture() {

        // }

        [Test]
        public void Test1() {
            Assert.Pass();
        }

        [Test]
        public void Test2() {
            Assert.That(false);
        }


    }
}
