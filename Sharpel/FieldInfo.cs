using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;




namespace Sharpel {

    public struct MemberInfo {
        public ExpressionSyntax valueExpression;
        public ISymbol sym;
        public ITypeSymbol type;
        public bool makeNullable;
        public string name;
        public string typeName;
    }

}
