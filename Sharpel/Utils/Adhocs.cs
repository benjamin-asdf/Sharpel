using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Sharpel {

    public static class Adhocs {

        public static bool AdHocParse(string input, out SyntaxTree tree, out Compilation compilation, out SemanticModel model) {

            tree = SyntaxFactory.ParseSyntaxTree(input, CSharpParseOptions.Default.WithPreprocessorSymbols("EDIT_CONST"));
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            compilation = CSharpCompilation.Create("bestCompilation",
                                                   syntaxTrees: new[] { tree }, references: new[] { mscorlib });

            model = compilation.GetSemanticModel(tree);
            return model != null && compilation != null;
        }
    }


}



