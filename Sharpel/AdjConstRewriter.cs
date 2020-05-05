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

        public string Rewrite(SyntaxNode root) {
            var classDeclaration = root.ChildNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (classDeclaration == null) {
                Console.Error.WriteLine("cannot get class declaration");
                return "";
            }

            var fieldInfos = GetFieldInfos(model,classDeclaration.Members);
            var adjClassName = $"{classDeclaration.Identifier.ValueText}Adj";
            var adjClass = AdjClassSyntax(classDeclaration,adjClassName,fieldInfos);
            var newConst = NewConst(classDeclaration,adjClassName,fieldInfos);

            return $"{newConst}\r\n{adjClass}";

            // return newConst.ToFullString();
        }

        static List<FieldInfo> GetFieldInfos(SemanticModel model, SyntaxList<MemberDeclarationSyntax> members) {
            var infos = new List<FieldInfo>();
            foreach (var member in members) {
                if (!(member is FieldDeclarationSyntax fieldDecl)) throw new Exception($"Unsupported member {member.GetType()}");
                if (fieldDecl.Declaration.Variables.Count() != 1) throw new Exception($"Unsupported member, unsupported number of variables. {member.GetType()}");
                var variable = fieldDecl.Declaration.Variables[0];

                var sym = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                if (sym == null) throw new Exception($"Unable to get sym from {variable}");

                // NOTE ignore backing fields already
                if (variable.Initializer == null) continue;

                infos.Add(new FieldInfo() {
                    sym = sym,
                    decl = fieldDecl,
                    variable = variable
                });

            }

            return infos;

        }

        ClassDeclarationSyntax AdjClassSyntax(
            ClassDeclarationSyntax old, string adjClassName, List<FieldInfo> fieldInfos) {
            var adjClass = old
                .WithIdentifier(Identifier(adjClassName))
                .WithMembers(AdjMembers(fieldInfos)).NormalizeWhitespace()
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



        static SyntaxToken questionToken = SyntaxFactory.Token(SyntaxKind.QuestionToken);

        SyntaxList<MemberDeclarationSyntax> AdjMembers(List<FieldInfo> fieldInfos) {
            var list = new List<MemberDeclarationSyntax>();
            foreach (var field in fieldInfos) {
                TypeSyntax newType = null;

                // TODO special types defined in attributes
                if (field.sym.Type.IsValueType) {
                    if (!(field.decl.Declaration.Type is PredefinedTypeSyntax predefinedTypeSyntax)) {
                        Console.WriteLine("{field.decl} - value type but not predefinedTypeSyntax.");
                        continue;
                    }

                    newType = SyntaxFactory.NullableType(
                        PredefinedType(Token(predefinedTypeSyntax.Keyword.Kind())));

                } else {
                    newType = field.decl.Declaration.Type;
                }

                if (newType == null) {
                    throw new Exception("Unsupported field type {field.ToFullString()}");
                }

                list.Add(buildFieldDecl(newType,field.variable.Identifier,SyntaxKind.PublicKeyword));

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


        static SyntaxTokenList publicStaticModifiers {
            get {
                var list = SyntaxTokenList.Create(Token(SyntaxKind.PublicKeyword));
                list = list.Add(Token(SyntaxKind.StaticKeyword));
                return list;
            }
        }

        ClassDeclarationSyntax NewConst(ClassDeclarationSyntax oldClass, string adjClassName, List<FieldInfo> fieldInfos) {
            var newMembers = new List<MemberDeclarationSyntax>();

            foreach (var field in fieldInfos) {

                static FieldDeclarationSyntax buildField(FieldDeclarationSyntax field,ExpressionSyntax initializerValue, VariableDeclaratorSyntax variable) {

                    return field
                            .ReplaceNode(variable.Initializer,
                                SyntaxFactory.EqualsValueClause(initializerValue).NormalizeWhitespace())
                            .WithModifiers(publicStaticModifiers).NormalizeWhitespace();
                }

                var variable = field.variable;
                var fieldSyntax = field.decl;
                var sym = field.sym;
                var valueExpr = ValueExpression(variable.Initializer.Value);


                if (fieldSyntax.Declaration.Type is PredefinedTypeSyntax predefinedType
                    || sym.Type.IsValueType) {
                    var expr = coaleseExpr(accessIExpression(adjClassName,sym.Name),valueExpr);
                    newMembers.Add(buildField(fieldSyntax,expr,variable));
                } else if (sym.Type.IsReferenceType) {
                    var backingFieldName = $"__{variable.Identifier.ValueText}__";
                    var backingField = buildFieldDecl(
                        fieldSyntax.Declaration.Type,
                        backingFieldName,
                        SyntaxKind.StaticKeyword);


                    var expr = coaleseExpr(
                        accessIExpression(adjClassName,sym.Name),
                        // (__array__ ?? (__array__ = new []{1,2,3})
                        coaleseExpr(IdentifierName(backingFieldName),
                                    ParenthesizedExpression(AssignmentExpression(
                                                                SyntaxKind.SimpleAssignmentExpression,
                                                                IdentifierName(backingFieldName),valueExpr))));

                    newMembers.Add(backingField);
                    newMembers.Add(buildField(fieldSyntax,expr,variable));

                } else {
                    Console.WriteLine("dont know how to handle {field.Name} {field.Type}.");
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

    public static class CasinoConst /*even more best */ {
        public const int CASINO_SLOT_AMOUNT = 8;
        public const long casinoRefreshPrice = 230000;
        public const string str = "alskdj" + "as;dfj";
        static int[] _array;
        // these are what I can expect
        public static int[] array = _array ?? (_array = new []{1,2,3});
        public static readonly int[] bestArr = { 1,2,3 };
    }



}


// TODO could try for formatting
// // var workspace = MSBuildWorkspace.Create();
// var workspace = new AdhocWorkspace();
// // var options = new OptionSet().WithChangedOption(Option<T>(string feature, string name), T value);
// var options = new FormattingOptions().;
// Console.WriteLine(options);
// Formatter.Format(adjClass,workspace,);
