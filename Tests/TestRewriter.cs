using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Sharpel;

namespace Tests {

    public class TestRewriter {
        const string outFile = "out";
        void Log(string obj) {
            File.AppendAllText(outFile,$"{obj}\n");
        }

        static string WithoutWhiteSpace(string input) {
            return Regex.Replace(input, @"\s+", "");
        }

        [Test]
        public void SimpleValueField() {

            const string simpleValueField =
                @"
public static class CasinoConst {
    public const int CASINO_SLOT_AMOUNT = 8;
}
";

            const string expected = @"
public static class CasinoConst
{
    public static int CASINO_SLOT_AMOUNT = CasinoConstAdj.I.CASINO_SLOT_AMOUNT ?? 8;
}
public static class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
{
    int? CASINO_SLOT_AMOUNT;
}
";
            AssertRewriteNoWhiteSpace(simpleValueField,expected);

        }


        [Test]
        public void TestProperty() {

            AssertRewriteNoWhiteSpace(
                @"
public static class CasinoConst  {
    static int[] _array;
    public static int[] array => _array ?? (_array = new []{1,2,3});
}
"
               ,

                @"
public static class CasinoConst {
    static int[] __array__;
    public static int[] array = CasinoConstAdj.I.array ?? (__array__ ?? (__array__ = new []{1,2,3}));
}
public class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj> {
    public int[] array;
}
"
               );

        }

        static void AssertRewriteNoWhiteSpace(string input, string expected) {
            if (Adhocs.AdHocParse(input, out SyntaxTree tree, out Compilation compilation, out SemanticModel model)) {
                var root = tree.GetRoot();
                var rewriter = new AdjConstRewriter(compilation,model);
                var output = rewriter.Rewrite(root);
                Assert.AreEqual(WithoutWhiteSpace(expected), WithoutWhiteSpace(output));
            }
        }

    }
}
