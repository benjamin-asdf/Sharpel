using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpel {

    public class AdjConstRewriter {
        readonly Compilation compilation;
        readonly SemanticModel model;

        public AdjConstRewriter(Compilation compilation, SemanticModel model) {
            this.compilation = compilation;
            this.model = model;
        }

        static SyntaxTriviaList ifEditConst = SyntaxFactory.ParseLeadingTrivia("\n\r#if EDIT_CONST\n\r");
        static SyntaxTriviaList elseConst = SyntaxFactory.ParseTrailingTrivia("#else\n\r");
        static SyntaxTriviaList endifConst = SyntaxFactory.ParseTrailingTrivia("#endif //EDIT_CONST\n\r");

        public string Rewrite(SyntaxNode root) {

            string outstring = "";

            var classDeclaration = root.ChildNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            var usingDirectives = root.ChildNodes().OfType<UsingDirectiveSyntax>();

            if (classDeclaration == null) {
                Console.Error.WriteLine("cannot get class declaration");
                return "";
            }

            foreach (var usingDirective in usingDirectives) {
                outstring = $"{outstring}{usingDirective.ToFullString()}";
            }

            var memberInfos = GetMemberInfos(model,classDeclaration.Members);
            var adjClassName = $"{classDeclaration.Identifier.ValueText}Adj";
            var adjClass = AdjClassSyntax(classDeclaration,adjClassName,memberInfos);
            var newConst = NewConst(classDeclaration,adjClassName,memberInfos).WithoutTrivia();
            foreach (var mem in memberInfos) {
                Console.WriteLine($"{mem.sym.Name} make nullable {mem.makeNullable}");
            }

            return $"{outstring}\n#if EDIT_CONST\n{classDeclaration.WithoutTrivia()}\n#else\n{newConst}\n{adjClass}\n#endif //EDIT_CONST".Replace("\r\n", "\n");
            }

            static List<MemberInfo> GetMemberInfos(SemanticModel model, SyntaxList<MemberDeclarationSyntax> members) {
            var infos = new List<MemberInfo>();
            foreach (var member in members) {
                if (member is PropertyDeclarationSyntax propertyDecl) {

                    var sym = model.GetDeclaredSymbol(propertyDecl) as IPropertySymbol;
                    if (sym == null) throw new Exception($"Unable to get sym from {propertyDecl}");

                    infos.Add(new MemberInfo() {
                            sym = sym,
                            valueExpression = ValueExpression(propertyDecl.ExpressionBody.Expression),
                            makeNullable = makeNullable(sym.Type,propertyDecl.Type),
                            type = sym.Type,
                            name = sym.Name,
                            typeName = sym.Type.ToString()
                        });

                } else if (member is FieldDeclarationSyntax fieldDecl) {
                    if (fieldDecl.Declaration.Variables.Count() != 1) throw new Exception($"Unsupported member, unsupported number of variables. {member.GetType()}");
                    var variable = fieldDecl.Declaration.Variables[0];

                    var sym = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                    if (sym == null) throw new Exception($"Unable to get sym from {variable}");

                    // NOTE ignore backing fields already
                    if (variable.Initializer == null) continue;

                    // if error, make nullabel
                    // TODO we have an attribute lookup
                    // if predefinedTypeSyntax -> make nullable, except string


                    infos.Add(new MemberInfo() {
                            sym = sym,
                            valueExpression = ValueExpression(variable.Initializer.Value),
                            makeNullable = makeNullable(sym.Type,fieldDecl.Declaration.Type),
                            type = sym.Type,
                            name = sym.Name,
                            typeName = sym.Type.ToString()
                        });
                } else {
                    Console.WriteLine($"Warning Unsupported member {member.GetType()}");
                }

                static bool makeNullable(ITypeSymbol type, TypeSyntax typeSyntax) {
                    return type.TypeKind == TypeKind.Error
                        || (typeSyntax is PredefinedTypeSyntax && type.MetadataName != "String");
                }

            }

            return infos;

        }

        ClassDeclarationSyntax AdjClassSyntax(
            ClassDeclarationSyntax old, string adjClassName, List<MemberInfo> memberInfos) {
            var adjClass = old
                .WithIdentifier(Identifier(adjClassName))
                .WithMembers(AdjMembers(memberInfos)).NormalizeWhitespace()
                .WithBaseList(
                    GetAdjBaseList(adjClassName)
                    ).NormalizeWhitespace(); // TODO bracket is on new line

            return adjClass;

        }


        BaseListSyntax GetAdjBaseList(string adjClassName) {
            const string constPatchClassName = "ConstantPatches";
            const string constAdjId = "ConstAdjustment";
            return BaseList(
                SingletonSeparatedList<BaseTypeSyntax>(
                    SimpleBaseType(
                        QualifiedName(
                            IdentifierName(constPatchClassName),
                            GenericName(
                                Identifier(constAdjId))
                            .WithTypeArgumentList(
                                TypeArgumentList(
                                    SingletonSeparatedList<TypeSyntax>(
                                        IdentifierName(adjClassName))))))));
        }

        SyntaxList<MemberDeclarationSyntax> AdjMembers(List<MemberInfo> memberInfos) {
            var list = new List<MemberDeclarationSyntax>();
            foreach (var member in memberInfos) {
                TypeSyntax newType = member.makeNullable ?
                    nullableType(member.typeName)
                    : IdentifierName(member.typeName);

                list.Add(buildFieldDecl(newType,member.sym.Name,SyntaxKind.PublicKeyword));
            }
            return new SyntaxList<MemberDeclarationSyntax>(list);
        }

        static ExpressionSyntax ValueExpression(ExpressionSyntax initializerExpression) {
            if ((initializerExpression is BinaryExpressionSyntax binaryExpression)
                && binaryExpression.Kind() == SyntaxKind.CoalesceExpression) {

                var right = binaryExpression.Right;
                if (right is ParenthesizedExpressionSyntax parenthesizedExpression
                    && parenthesizedExpression.Expression is AssignmentExpressionSyntax assignment) {
                    return assignment.Right;
                }
                return binaryExpression.Right;
            }

            return initializerExpression;

        }

        // public static int CASINO_SLOT_AMOUNT => CasinoConstAdj.I.CASINO_SLOT_AMOUNT ?? 8;
        // public static long casinoRefreshPrice => CasinoConstAdj.I.casinoRefreshPrice ?? 230000;
        // static int[] __array__;
        // public static int[] array => CasinoConstAdj.I.array ?? (__array__ ?? (__array__ = new []{1,2,3}));

        ClassDeclarationSyntax NewConst(ClassDeclarationSyntax oldClass, string adjClassName, List<MemberInfo> memberInfos) {
            var newMembers = new List<MemberDeclarationSyntax>();
            foreach (var mem in memberInfos) {

                // string is the only value type without backing field?
                // NOTE unkown tyes also return false for IsValueType
                if (mem.type.IsValueType && mem.type.Name != "String") {
                    // make backing field

                    var backingFieldName = $"__{mem.name}__";
                    var backingField = buildFieldDecl(
                        IdentifierName(mem.typeName),
                        backingFieldName,
                        SyntaxKind.StaticKeyword);

                    var expr = coaleseExpr(
                        accessIExpression(adjClassName,mem.name),
                        // (__array__ ?? (__array__ = new []{1,2,3})
                        coaleseExpr(IdentifierName(backingFieldName),
                                    ParenthesizedExpression(AssignmentExpression(
                                                                SyntaxKind.SimpleAssignmentExpression,
                                                                IdentifierName(backingFieldName),mem.valueExpression))));

                    newMembers.Add(backingField);
                    newMembers.Add(buildProp(mem.typeName,mem.name,expr));

                } else {

                    var expr = coaleseExpr(accessIExpression(adjClassName,mem.name),
                                           mem.valueExpression);

                    newMembers.Add(buildProp(mem.typeName,mem.name,expr));

                }


                static ExpressionSyntax coaleseExpr(ExpressionSyntax left, ExpressionSyntax right) {
                    return BinaryExpression(SyntaxKind.CoalesceExpression,left,Token(SyntaxKind.QuestionQuestionToken),right);

                }

                static ExpressionSyntax accessIExpression(string className, string identifier) {
                    return MemberAccessExpression(
                        // CasinoConstAdj.I.name
                        SyntaxKind.SimpleMemberAccessExpression,
                        // CasinoConstAdj.I
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(className),
                            Token(SyntaxKind.DotToken),
                            IdentifierName("I")),
                        Token(SyntaxKind.DotToken),
                        IdentifierName(identifier));
                }
            }

            static PropertyDeclarationSyntax buildProp(string typeName, string name, ExpressionSyntax expr) {
                return PropertyDeclaration(
                    IdentifierName(typeName),
                    Identifier(name))
                    .WithModifiers(modifierList(SyntaxKind.PublicKeyword,SyntaxKind.StaticKeyword))
                    .WithExpressionBody(
                        ArrowExpressionClause(expr))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            }

            return oldClass.WithMembers(new SyntaxList<MemberDeclarationSyntax>(newMembers))
                // NOTE radical.
                .WithoutTrivia()
                .NormalizeWhitespace();
        }


        static FieldDeclarationSyntax buildFieldDecl(TypeSyntax type, string identifier, params SyntaxKind[] modifierKinds) {
            return buildFieldDecl(type,Identifier(identifier),modifierKinds);
        }

        static FieldDeclarationSyntax buildFieldDecl(TypeSyntax type, SyntaxToken identifier, params SyntaxKind[] modifierKinds) {
            return FieldDeclaration(
                VariableDeclaration(type,
                                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                                        VariableDeclarator(identifier.NormalizeWhitespace()))))
                .WithModifiers(modifierList(modifierKinds));
        }


        static SyntaxTokenList modifierList(params SyntaxKind[] modifierKinds) {
            var list = TokenList();
            foreach (var kind in modifierKinds) {
                list.Add(Token(kind));
            }
            return list;
        }

        static TypeSyntax nullableType(string name) {
            return NullableType(IdentifierName(name));
        }

    }

}


// TODO could try for formatting
// // var workspace = MSBuildWorkspace.Create();
// var workspace = new AdhocWorkspace();
// // var options = new OptionSet().WithChangedOption(Option<T>(string feature, string name), T value);
// var options = new FormattingOptions().;
// Console.WriteLine(options);
// Formatter.Format(adjClass,workspace,);
