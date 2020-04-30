using System;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Sharpel {

    class Program {

        static void Main(string[] args) {
            Console.WriteLine("Running BenjRoslyn, listening for input...");
            
            while (true) {
                var command = Console.ReadLine();
                if (String.IsNullOrEmpty(command) || command != ":filename:") {
                    Console.WriteLine("Invalid command.");
                } else {
                    var filename = Console.ReadLine();
                    var fileContents = File.ReadAllText(filename); // todo retry logic
                    CheckClassDeclatation(fileContents);
                        
                }
                
            }
        }

        static void CheckClassDeclatation(string input) {
            if (!String.IsNullOrEmpty(input)) {
                var tree = SyntaxFactory.ParseSyntaxTree(input, CSharpParseOptions.Default.WithPreprocessorSymbols("EDIT_CONST"));
                var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                var compilation = CSharpCompilation.Create("bestCompilation",
                                                           syntaxTrees: new[] { tree }, references: new[] { mscorlib });


                var root = tree.GetRoot();
                var classDeclaration = root.ChildNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

                if (classDeclaration == null) {
                    Console.Error.WriteLine("cannot get class declaration");
                    Console.Error.WriteLine(input);
                    return;
                }


                var model = compilation.GetSemanticModel(tree);
                foreach (var memberDeclarationSyntax in classDeclaration.Members) {
                    Console.WriteLine(memberDeclarationSyntax.ToFullString());
                    foreach (var item in memberDeclarationSyntax.DescendantNodes()) {
                        Console.WriteLine(item.Kind());
                    }
                }



            }
        }







        static Compilation GetCompilation(string input) {
            var tree = SyntaxFactory.ParseSyntaxTree(input, CSharpParseOptions.Default.WithPreprocessorSymbols("EDIT_CONST"));
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var compilation = CSharpCompilation.Create("bestCompilation",
                                                       syntaxTrees: new[] { tree }, references: new[] { mscorlib });
            // var model = compilation.GetSemanticModel(tree);


            return compilation;
        }

    }
}

// using (var ir = Console.OpenStandardInput()) {
//     while (true) {
//         var input = ir.Read(Span<byte> buffer)
//         Console.WriteLine("input is");
//         Console.WriteLine(input);
//         CheckClassDeclatation(input);

//     }
// }

// using (var ir = Console.In) {
//     while (true) {
//         var input = ir.ReadToEnd();
//         Console.WriteLine("input is");
//         Console.WriteLine(input);
//         CheckClassDeclatation(input);

//     }
// }
