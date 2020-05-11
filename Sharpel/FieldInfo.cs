using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;




namespace Sharpel
{

    public struct MemberInfo {
        public IFieldSymbol fieldSym;
        public IPropertySymbol propSym;
        public bool isField;
        public bool isProperty;
        public FieldDeclarationSyntax fieldDecl;
        public VariableDeclaratorSyntax variable;
        public PropertyDeclarationSyntax propDecl;
        public string name;
        public ITypeSymbol type;



        // try with only this
        public ExpressionSyntax valueExpression;
        public ISymbol sym;
        public bool makeNullable;

    }


    // we only need the ISymbol and the initializerExpression ?


}
