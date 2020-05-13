using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Sharpel;

namespace Tests {

    public class TestRewriter {

        const string emptyClassDecl = "public static class CasinoConst  {}";


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
#if EDIT_CONST
public static class CasinoConst {
    public const int CASINO_SLOT_AMOUNT = 8;
}
#else
public static class CasinoConst
{
    public static int CASINO_SLOT_AMOUNT => CasinoConstAdj.I.CASINO_SLOT_AMOUNT ?? 8;
}
public static class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
{
    public int? CASINO_SLOT_AMOUNT;
}
#endif //EDIT_CONST
";
            AssertRewriteNoWhiteSpace(simpleValueField,expected);

        }


        [Test]
        public void TestArray() {

            AssertRewriteNoWhiteSpace(
                @"
public static class CasinoConst  {
    static int[] _array;
    public static int[] array => _array ?? (_array = new []{1,2,3});
}
"
               ,

                @"
#if EDIT_CONST
public static class CasinoConst  {
    static int[] _array;
    public static int[] array => _array ?? (_array = new []{1,2,3});
}
#else
public static class CasinoConst
{
    static int[] __array__;
    public static int[] array => CasinoConstAdj.I.array ?? __array__ ?? (__array__ = new[]{1, 2, 3});
}
public static class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
{
    public int[] array;
}
#endif //EDIT_CONST
"
               );

        }

//         [Test]
//         public void TestStringLiteral() {

//             AssertRewriteNoWhiteSpace(@"
// public static class CasinoConst  {
//     public string hi => ""asldjf"";
// }
// ",
// @"
// #if EDIT_CONST
// public static class CasinoConst  {
//     public string hi => ""asldjf"";
// }
// #else
// public static class CasinoConst
// {
//     public static string hi => CasinoConstAdj.I.hi ?? ""asldjf"";
// }
// public static class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
// {
//     public string hi;
// }
// #endif //EDIT_CONST
// "
// );
//         }


        [Test]
        public void TestProperty() {

            AssertRewriteNoWhiteSpace(@"
public static class CasinoConst  {
    public int bestProp => 88;
}
",@"
#if EDIT_CONST
public static class CasinoConst  {
    public int bestProp => 88;
}
#else
public static class CasinoConst
{
    public static int bestProp => CasinoConstAdj.I.bestProp ?? 88;
}
public static class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
{
    public int? bestProp;
}
#endif //EDIT_CONST
");
        }

// todo
        // test hashset
        // test prop with initializer
        // test timespan

        [Test]
        public void TestPropWithInitializer() {

            AssertRewriteNoWhiteSpace(@"
public static class CasinoConst  {
    public static int best { get; } = 4;
}
",@"
#if EDIT_CONST
public static class CasinoConst  {
    public int best => 88;
}
#if EDIT_CONST
public static class CasinoConst  {
    public static int best { get; } = 4;
}
#else
public static class CasinoConst
{
    public static int best => CasinoConstAdj.I.best ?? 4;
}
public static class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
{
    public int? best;
}
#endif //EDIT_CONST
");

        }

        [Test]
        public void TestUsings() {
            const string bestUsing = "using System;\n";
            const string input = bestUsing + emptyClassDecl;

            if (Adhocs.AdHocParse(input, out SyntaxTree tree, out Compilation compilation, out SemanticModel model)) {
                var root = tree.GetRoot();
                var rewriter = new AdjConstRewriter(compilation,model);
                var output = rewriter.Rewrite(root);
                Assert.That(output.Contains(bestUsing));
            }

        }

        [Test]
        public void TestPreprocessorTrivia() {

            if (Adhocs.AdHocParse(emptyClassDecl, out SyntaxTree tree, out Compilation compilation, out SemanticModel model)) {
                var root = tree.GetRoot();
                var rewriter = new AdjConstRewriter(compilation,model);
                var output = rewriter.Rewrite(root);
                foreach (var s in new [] {"#if EDIT_CONST", "#else", "#endif //EDIT_CONST"}) {
                    Assert.That(new Regex(s).Matches(output).Count == 1);
                }
            }
        }


        [Test]
        public void TestGenerics() {
            AssertRewriteNoWhiteSpace(
                @"
public static class MenuConst {
    private static HashSet<OverlayType> _stackableOverlays;
    public static HashSet<OverlayType> stackableOverlays => _stackableOverlays ?? (_stackableOverlays = new HashSet<OverlayType>{
        OverlayType.LiveChallenges,
    });
}
",@"
#if EDIT_CONST
public static class MenuConst {
    private static HashSet<OverlayType> _stackableOverlays;
    public static HashSet<OverlayType> stackableOverlays => _stackableOverlays ?? (_stackableOverlays = new HashSet<OverlayType>{
        OverlayType.LiveChallenges,
    });
}
#else
public static class MenuConst
{
    static HashSet<OverlayType> __stackableOverlays__;
    public static HashSet<OverlayType> stackableOverlays => MenuConstAdj.I.stackableOverlays ?? __stackableOverlays__ ?? (__stackableOverlays__ = new HashSet<OverlayType>{OverlayType.LiveChallenges, });
}
public static class MenuConstAdj : ConstantPatches.ConstAdjustment<MenuConstAdj>
{
    public HashSet<OverlayType> stackableOverlays;
}
#endif //EDIT_CONST
"
                );
        }

        [Test]
        public void TestClassFromSkelletonDll() {

            AssertRewriteNoWhiteSpace(@"
public static class MenuConst {
    public Num bestNum => 99;
}
",@"
#if EDIT_CONST
public static class MenuConst {
    public Num bestNum => 99;
}
#else
public static class MenuConst
{
    static Num __bestNum__;
    public static Num bestNum => MenuConstAdj.I.bestNum ?? __bestNum__ ?? (__bestNum__ = 99);
}
public static class MenuConstAdj : ConstantPatches.ConstAdjustment<MenuConstAdj>
{
    public Num bestNum;
}
#endif //EDIT_CONST
");
        }


        // test frozen collection







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
