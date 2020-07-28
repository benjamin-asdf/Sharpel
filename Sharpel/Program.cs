using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using Microsoft.CodeAnalysis;
using Utils;
using AdjConstRewriter;

namespace Sharpel {

    class Program {

        public enum Command {
            None,
            Filename,
            LogSyntax,
            RewriteFile
        }


        static void Main(string[] args) {
            // todo nice command parsing

            Console.WriteLine(args.Length);

            if (args.Length == 2 && args[0] == "--split-classes") {
                Console.WriteLine($"split classes! arg:");
                Console.WriteLine(args[1]);
                ClassSplit.Split(args[1]);
                return;
            }


            Console.WriteLine("Running Sharpel stdio mode., listening for input...");


            while (true) {
                Console.WriteLine("\ninput:\n");

                // could build the line

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
                        WithFileContents(input,LogSyntax);
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

                    // input = input.Replace('\0', '\n');
                    // var lines = input.Split('\n');
                    // Console.WriteLine($"have {lines.Count()} lines");
                    return !String.IsNullOrWhiteSpace(input);
                }

                return false;
            }
        }

        static void LogSyntax(string path, string input) {
            var tree = SyntaxFactory.ParseSyntaxTree(input);
            var root = tree.GetRoot();
            LogWithIndent(1,root);


            void LogWithIndent(int level, SyntaxNode node) {
                var pad = new String('*',level);

                // foreach (var item in node.DescendantNodesAndTokens(null,true)) {
                //         Console.WriteLine($"{pad} {item.Kind()} - {item.ToFullString()}");
                //     }

                // }
                foreach (var item in node.ChildNodesAndTokens()) {
                    if (level == 1) {
                        Console.WriteLine("-----------------");
                    }
                    var s = item.ToFullString();
                    var shortend = s.Length > 40 ? s.Substring(0, 40) : s;
                    var previewPart = shortend.Replace('\t', ' ').Replace("\r\n"," ");
                    Console.WriteLine($"{pad} {item.Kind()} - {previewPart}\n{item.ToFullString()}");
                    var childNode = item.AsNode();
                    if (childNode != null) {
                        // Console.WriteLine($"{pad}descendants: ({childNode.DescendantNodes().Count()})");
                        LogWithIndent(level + 1,childNode);
                    }
                }
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
            var rewriter = new Rewriter(input);
            return rewriter.Rewrite();
        }
    }
}
