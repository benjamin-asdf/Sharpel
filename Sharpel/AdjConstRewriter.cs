using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpel {

    public class AdjConstRewriter {

        Compilation compilation;
        SemanticModel model;

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

            var adjClassName = $"{classDeclaration.Identifier.ValueText}Adj";
            var adjClass = AdjClassSyntax(classDeclaration,adjClassName);
            var newConst = NewConst(classDeclaration,adjClassName);

            // return $"{newConst}\r\n{adjClass}";

            return newConst.ToFullString();
        }


        ClassDeclarationSyntax AdjClassSyntax(
            ClassDeclarationSyntax old, string adjClassName) {
            var adjClass = old
                .WithIdentifier(Identifier(adjClassName)
                                // .WithTriviaFrom(old.Identifier)
                    )
                .WithMembers(AdjMembers(old.Members)).NormalizeWhitespace()
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

        SyntaxList<MemberDeclarationSyntax> AdjMembers(SyntaxList<MemberDeclarationSyntax> oldMembers) {
            var list = new List<MemberDeclarationSyntax>();

            foreach (var oldMember in oldMembers) {
                if (oldMember is FieldDeclarationSyntax field) {
                    TypeSyntax newType = null;
                    if (field.Declaration.Variables.Count != 1) {
                        throw new Exception($"Unsupported field. {field.ToFullString()}");
                    }

                    if (field.Declaration.Type is PredefinedTypeSyntax predefinedType) {
                        // TODO only make nullables nullable
                        newType = SyntaxFactory.NullableType(PredefinedType(Token(predefinedType.Keyword.Kind())));
                    } else
                        // if (field.Declaration.Type is IdentifierNameSyntax identifier) {
                        // }

                    newType = field.Declaration.Type;
                    if (newType == null) {
                        throw new Exception("Unsupported field type {field.ToFullString()}");
                    }

                    list.Add(AdjFieldDecl(field,newType));
                }
            }
            return new SyntaxList<MemberDeclarationSyntax>(list);
        }


        FieldDeclarationSyntax AdjFieldDecl(FieldDeclarationSyntax field, TypeSyntax type) {
            // NOTE: only support 1 variable
            var variable = field.Declaration.Variables[0];
            return field.WithDeclaration(
                field.Declaration
                .WithType(type)
                .WithVariables(
                    SingletonSeparatedList<VariableDeclaratorSyntax>(
                        VariableDeclarator(variable.Identifier)))
                )
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
                // .WithTriviaFrom(field);

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

        ClassDeclarationSyntax NewConst(ClassDeclarationSyntax oldClass, string adjClassName) {
            var newMembers = new List<MemberDeclarationSyntax>();

            foreach (var member in oldClass.Members) {
                if (!(member is FieldDeclarationSyntax fieldSyntax)) throw new Exception("not supportet {member.Kind()} - {member}");
                var variable = fieldSyntax.Declaration.Variables[0];
                if (variable.Initializer == null) {
                    Console.WriteLine($"{variable} doesnt have an initializer");
                    continue;
                }

                var sym = model.GetDeclaredSymbol(variable);
                if (sym == null) {
                    Console.WriteLine("couldnt get sym form {variable}");
                    continue;
                }

                if (!(sym is IFieldSymbol field))  {
                    Console.WriteLine("not abel to get field symbol {member}");
                    continue;

                }

                static FieldDeclarationSyntax buildField(FieldDeclarationSyntax field,ExpressionSyntax initializerValue, VariableDeclaratorSyntax variable) {

                    return field
                            .ReplaceNode(variable.Initializer,
                                SyntaxFactory.EqualsValueClause(initializerValue).NormalizeWhitespace())
                            .WithModifiers(publicStaticModifiers).NormalizeWhitespace();
                }

                var valueExpr = ValueExpression(variable.Initializer.Value);

                Console.WriteLine($"{sym.Name} - value type: {field.Type.IsValueType} {field.Type}");

                if (field.Type.IsValueType) {
                    var expr = BuildCoalseceExpression(adjClassName,variable,valueExpr);
                    newMembers.Add(buildField(fieldSyntax,expr,variable));

                } else {
                    // reference type
                    var backingFieldName = $"__{variable.Identifier.ValueText}__";
                    var backingField =
                        fieldSyntax.WithDeclaration(
                            SyntaxFactory.VariableDeclaration(
                                fieldSyntax.Declaration.Type,
                                SeparatedList<VariableDeclaratorSyntax>(
                                    new [] {SyntaxFactory.VariableDeclarator(Identifier(backingFieldName).NormalizeWhitespace())})));


                    newMembers.Add(backingField);




                }



                // field.Type.IsValueType

                // get the value part of the declaration

                // var value



            }


            return oldClass.WithMembers(new SyntaxList<MemberDeclarationSyntax>(newMembers));
        }











        ExpressionSyntax BuildCoalseceExpression(string adjClassName, VariableDeclaratorSyntax variable, ExpressionSyntax right) {

            return BinaryExpression(SyntaxKind.CoalesceExpression,
                              // CasinoConstAdj.I.CASINO_SLOT_AMOUNT
                              MemberAccessExpression(
                                  SyntaxKind.SimpleMemberAccessExpression,
                                  // CasinoConstAdj.I
                                  MemberAccessExpression(
                                      SyntaxKind.SimpleMemberAccessExpression,
                                      IdentifierName(adjClassName),
                                      Token(SyntaxKind.DotToken),
                                      IdentifierName("I")),
                                  Token(SyntaxKind.DotToken),
                                  IdentifierName(variable.Identifier)),
                              Token(SyntaxKind.QuestionQuestionToken),
                              right);

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
