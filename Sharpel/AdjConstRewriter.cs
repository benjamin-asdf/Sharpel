using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace Sharpel {
    
    public class AdjConstRewriter {

        public SyntaxNode Rewrite(SyntaxNode root, Compilation compilation, SemanticModel model) {
                var classDeclaration = root.ChildNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDeclaration == null) {
                    Console.Error.WriteLine("cannot get class declaration");
                    return root;
                }

                foreach (var memberDeclarationSyntax in classDeclaration.Members) {
                    // Console.WriteLine(memberDeclarationSyntax.ToFullString());
                    // Console.WriteLine($"is a {memberDeclarationSyntax.Kind()}");

                    // foreach (var item in memberDeclarationSyntax.DescendantNodes()) {
                    //     Console.WriteLine(item.Kind());
                    // }

                }



                var adjClassName = $"{classDeclaration.Identifier.ValueText}Adj";

                return AdjClassSyntax(classDeclaration,adjClassName);

        }


        static ClassDeclarationSyntax AdjClassSyntax(
            ClassDeclarationSyntax old, string adjClassName) {
            var adjId = SyntaxFactory.Identifier(adjClassName).WithTriviaFrom(old.Identifier);
            var newClass = old.WithIdentifier(adjId).WithMembers(AdjMembers(old.Members));


            return newClass;
            
        }

        static SyntaxToken questionToken = SyntaxFactory.Token(SyntaxKind.QuestionToken);

        static SyntaxList<MemberDeclarationSyntax> AdjMembers(SyntaxList<MemberDeclarationSyntax> oldMembers) {
            var list = new List<MemberDeclarationSyntax>();

            foreach (var oldMember in oldMembers) {


                if (oldMember is FieldDeclarationSyntax field) {
                    Console.WriteLine($"var decl: {field.Declaration}");
                    Console.WriteLine($"type kind: {field.Declaration.Type.Kind()}");


                    if (field.Declaration.Type is PredefinedTypeSyntax predefinedType) {


                        var keyword = predefinedType.Keyword.ValueText;

                        // var newType = SyntaxFactory.NullableType(keyword);




                    }


                }

                var trivia = SyntaxFactory.ParseTrailingTrivia("\r\nheheehehe mfs\r\n");
                Console.WriteLine($"trivia: {trivia.FirstOrDefault().ToFullString()}");

                var newMember = oldMember
                    .WithTrailingTrivia(trivia);
                list.Add(newMember);




                Console.WriteLine($"new member... {newMember.ToFullString()}");


            }

            return new SyntaxList<MemberDeclarationSyntax>(list);


        }

        // var declaration
        // name => old name
        // no initialzier
        // convert type to nullable type





        // static VariableDeclarationSyntax SimpleAdjConstVariableSyntax(VariableDeclarationSyntax oldDecl) {
        //     if (oldDecl.Type is PredefinedTypeSyntax predefinedType) {
        //         return oldDecl.WithType();

        //     }



        // }

        // static MemberDeclarationSyntax SimpleAdjConstMember(MemberDeclarationSyntax oldMember) {
        //     var type = oldMember.


        // }

























        public SyntaxNode _Rewrite(SyntaxNode root) {
            var clOld = root.ChildNodes().First(n => n is ClassDeclarationSyntax);
            var clNew = clOld
                .WithLeadingTrivia(ifEditConst)
                .WithTrailingTrivia(elseConst.AddRange(endifConst).Insert(0, SyntaxFactory.CarriageReturnLineFeed));
            return root.ReplaceNode(clOld, clNew);
        }

        static SyntaxTriviaList ifEditConst = SyntaxFactory.ParseLeadingTrivia("\n\r#if EDIT_CONST\n\r");
        static SyntaxTriviaList elseConst = SyntaxFactory.ParseTrailingTrivia("#else\n\r");
        static SyntaxTriviaList endifConst = SyntaxFactory.ParseTrailingTrivia("#endif //EDIT_CONST\n\r");




        // SyntaxFactory.IfDirectiveTrivia(
        //     SyntaxFactory.IdentifierName("EDIT_CONST"),
        //     true,
        //     true,
        //     true
        // );

    }
}
