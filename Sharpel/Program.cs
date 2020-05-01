using System;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Sharpel {

    class Program {

        public enum Command {
            None,
            Filename,
            LogSyntax
        }

        static void Main(string[] args) {
            Console.WriteLine("Running Sharpel, listening for input...");
            
            while (true) {
                Console.WriteLine("\ninput:\n");


                // could build the line
                // var line = Console.ReadLine();
                // var lines = line.Split("\u0000");

                if (CommandInputLoop(out var cmd, out var input)) {

                    if (cmd == Command.Filename) {
                        var fileContents = File.ReadAllText(input); // todo retry logic
                        try {
                            CheckClassDeclatation(fileContents);
                        } catch (Exception e) {
                            Console.Error.WriteLine(e);
                        }
                    }

                    if (cmd == Command.LogSyntax) {
                        Console.WriteLine($"\nparse for log syntax...\n");
                        var tree = SyntaxFactory.ParseSyntaxTree(input);

                        var root = tree.GetRoot();
                        LogWithIndent(0,root);

                        void LogWithIndent(int level, SyntaxNode node) {
                            var pad = new String('*',level);
                            foreach (var item in node.ChildNodesAndTokens()) {
                                if (level == 0) {
                                    Console.WriteLine("-----------------");
                                }
                                Console.WriteLine($"{pad} {item.Kind()} - {item.ToFullString()}");
                                var childNode = item.AsNode();
                                if (childNode != null) {
                                    // Console.WriteLine($"{pad}descendants: ({childNode.DescendantNodes().Count()})");
                                    LogWithIndent(level + 1,childNode);
                                }
                            }
                        }
                    }



                } else {
                    Console.WriteLine("invalid input");
                }

            }

            bool CommandInputLoop(out Command cmd, out string input) {
                cmd = Command.None;
                input = "";
                var cmdInput = Console.ReadLine();
                if (!String.IsNullOrWhiteSpace(cmdInput)) {
                    if (cmdInput == ":filename:") {
                        cmd = Command.Filename;
                    }
                    if (cmdInput == ":logsyntax:") {
                        cmd = Command.LogSyntax;
                    }

                    input = Console.ReadLine();
                    // input = input.Replace("\u0000", "\r\n");
                    // Console.WriteLine($"have {input.Split("\r\n").Count()} lines");
                    return !String.IsNullOrWhiteSpace(input);
                }

                return false;
            }
        }

        static void CheckClassDeclatation(string input) {
            if (!String.IsNullOrEmpty(input)) {
                var tree = SyntaxFactory.ParseSyntaxTree(input, CSharpParseOptions.Default.WithPreprocessorSymbols("EDIT_CONST"));
                var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
                var compilation = CSharpCompilation.Create("bestCompilation",
                                                           syntaxTrees: new[] { tree }, references: new[] { mscorlib });


                var root = tree.GetRoot(); // best
                var rewriter = new AdjConstRewriter();
                var model = compilation.GetSemanticModel(tree);
                var newNode = rewriter.Rewrite(root,compilation,model);

                Console.WriteLine("--- output ---- ");
                Console.WriteLine(newNode);
                Console.WriteLine();
                Console.WriteLine("-----------");

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

// LogDesc(root);

// void LogDesc(SyntaxNode node) {
//     foreach (var n in node.DescendantNodes()) {
//         Console.WriteLine($"{n.Kind()} - {n.ToFullString()} - has {node.DescendantNodes().Count()} descendants");
//         LogDesc(n);
//     }
// }
