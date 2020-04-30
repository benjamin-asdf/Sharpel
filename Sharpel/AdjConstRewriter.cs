using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpel {
    
    public class AdjConstRewriter {
        public SyntaxNode Rewrite(SyntaxNode root) {
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