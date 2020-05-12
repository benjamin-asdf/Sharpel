using System;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Sharpel {

    class Program {

        public enum Command {
            None,
            Filename,
            LogSyntax,
            RewriteFile
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
                        Console.WriteLine("log rewrite... ");
                        WithFileContents(input,LogRewrite);
                    }

                    if (cmd == Command.RewriteFile) {
                        WithFileContents(input,RewriteFile);
                    }


                    if (cmd == Command.LogSyntax) {
                        Console.WriteLine($"\nparse for log syntax...\n");
                        var tree = SyntaxFactory.ParseSyntaxTree(input);

                        var root = tree.GetRoot();
                        LogWithIndent(0,root);

                        void LogWithIndent(int level, SyntaxNode node) {
                            var pad = new String('*',level);
                            // foreach (var item in node.DescendantNodesAndTokens(null,true)) {
                            //         Console.WriteLine($"{pad} {item.Kind()} - {item.ToFullString()}");
                            //     }

                            // }
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
                    if (cmdInput == ":rewrite-file:") {
                        cmd = Command.RewriteFile;
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

        static void WithFileContents(string path, Action<string,string> op) {
            var fileContents = "";
            Operation.Retry(5, () => {
                fileContents = File.ReadAllText(path);
            });
            try {
                op(path,fileContents);
            } catch (Exception e) {
                Console.Error.WriteLine(e);
            }

        }

        static void RewriteFile(string path, string content) {
            var newContent = GetRewrittenString(content);
            if (String.IsNullOrWhiteSpace(newContent)) {
                Console.WriteLine($"[Warning] writing null string to {path}\nInput follows\n\n{content}");

            }
            Operation.Retry(5,() => File.WriteAllText(path,newContent));
            Console.WriteLine($"\nSuccess.\nRewrote {path}");
        }

        static void LogRewrite(string path, string input) {
            if (!String.IsNullOrEmpty(input)) {
                Console.WriteLine();
                Console.WriteLine("--- input ---- ");
                Console.WriteLine(input);
                Console.WriteLine("\n");
                Console.WriteLine("--- output ---- ");
                Console.WriteLine(GetRewrittenString(input));
                Console.WriteLine();
                Console.WriteLine("-----------");
            }
        }


        static string GetRewrittenString(string input) {
            if (!String.IsNullOrEmpty(input)) {

                if (Adhocs.AdHocParse(input, out SyntaxTree tree, out Compilation compilation, out SemanticModel model)) {

                    var root = tree.GetRoot();
                    var rewriter = new AdjConstRewriter(compilation,model);
                    return rewriter.Rewrite(root);
                }
            }
            return "";
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
