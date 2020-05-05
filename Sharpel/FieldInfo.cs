using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;




namespace Sharpel {

    public struct FieldInfo {
        public IFieldSymbol sym;
        public FieldDeclarationSyntax decl;
        public VariableDeclaratorSyntax variable;
    }



}
