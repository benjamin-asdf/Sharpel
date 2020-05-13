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
public class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
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
public class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
{
    public int[] array;
}
#endif //EDIT_CONST
"
               );

        }

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
public class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
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
    public static int best { get; } = 4;
}
#else
public static class CasinoConst
{
    public static int best => CasinoConstAdj.I.best ?? 4;
}
public class CasinoConstAdj : ConstantPatches.ConstAdjustment<CasinoConstAdj>
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
public class MenuConstAdj : ConstantPatches.ConstAdjustment<MenuConstAdj>
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
public class MenuConstAdj : ConstantPatches.ConstAdjustment<MenuConstAdj>
{
    public Num bestNum;
}
#endif //EDIT_CONST
");
        }


        // test frozen collection

        [Test]
        public void TestPatchableList() {
            AssertRewriteNoWhiteSpace(@"
public static class ListConst {
    public static readonly PatchableList<string> List = new PatchableList<string>();
}
",@"
#if EDIT_CONST
public static class ListConst {
    public static readonly PatchableList<string> List = new PatchableList<string>();
}
#else
public static class ListConst
{
    public static PatchableList<string> List => ListConstAdj.I.List;
}
public class ListConstAdj : ConstantPatches.ConstAdjustment<ListConstAdj>
{
    static PatchableList<string> __listDefault__;
    public static PatchableList<string> __ListDefault__ => __listDefault__ ?? (__listDefault__ = new PatchableList<string>());
    public PatchableList<string> __ListAdj__;
    public static PatchableList<string> List
    {
        get => __ListAdj__ ?? __ListDefault__;
        set => __ListAdj__ = value;
    }
}
#endif //EDIT_CONST
");

        }




        [Test]
        public void TestUnknownType() {
            AssertRewriteNoWhiteSpace(@"
public static class BestConst {
    public SomeThingUnknown best = new SomeThingUnknown(10);
}
",@"
#if EDIT_CONST
public static class BestConst {
    public SomeThingUnknown best = new SomeThingUnknown(10);
}
#else
public static class BestConst
{
    static SomeThingUnknown __best__;
    public static SomeThingUnknown best => BestConstAdj.I.best ?? __best__ ?? (__best__ = new SomeThingUnknown(10));
}
public class BestConstAdj : ConstantPatches.ConstAdjustment<BestConstAdj>
{
    public SomeThingUnknown best;
}
#endif //EDIT_CONST
");


        }

        [Test]
        public void TestCustomStruct() {

            AssertRewriteNoWhiteSpace(@"
[CustomStruct(typeof(BestStruct))]
public static class BestConst {
    public BestStruct best = new BestStruct(10);
}
",@"
#if EDIT_CONST
[CustomStruct(typeof(BestStruct))]
public static class BestConst {
    public BestStruct best = new BestStruct(10);
}
#else
[CustomStruct(typeof(BestStruct))]
public static class BestConst
{
    public static BestStruct best => BestConstAdj.I.best ?? new BestStruct(10);
}
public class BestConstAdj : ConstantPatches.ConstAdjustment<BestConstAdj>
{
    public BestStruct? best;
}
#endif //EDIT_CONST
");


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
