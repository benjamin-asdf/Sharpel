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
        readonly CommonTypes types;

        public AdjConstRewriter(Compilation compilation, SemanticModel model) {
            this.compilation = compilation;
            this.model = model;
            this.types = new CommonTypes(compilation);
        }

        public string Rewrite(SyntaxNode root) {
            var outstring = "";
            var classDeclaration = root.ChildNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            var usingDirectives = root.ChildNodes().OfType<UsingDirectiveSyntax>();

            if (classDeclaration == null) {
                Console.Error.WriteLine("cannot get class declaration");
                return "";
            }

            foreach (var usingDirective in usingDirectives) {
                outstring = $"{outstring}{usingDirective.ToFullString()}";
            }

            var memberInfos = GetMemberInfos(model,classDeclaration.Members,types);
            var adjClassName = $"{classDeclaration.Identifier.ValueText}Adj";
            var adjClass = AdjClassSyntax(classDeclaration,adjClassName,memberInfos);
            var newConst = NewConst(classDeclaration,adjClassName,memberInfos).WithoutTrivia();
            foreach (var mem in memberInfos) {
                Console.WriteLine($"{mem.sym.Name} make nullable {mem.makeNullable}");
            }

            return $"{outstring}\n#if EDIT_CONST\n{classDeclaration.WithoutTrivia()}\n#else\n{newConst}\n{adjClass}\n#endif //EDIT_CONST".Replace("\r\n", "\n");
            }

            static List<MemberInfo> GetMemberInfos(SemanticModel model, SyntaxList<MemberDeclarationSyntax> members, CommonTypes types) {
                var infos = new List<MemberInfo>();
                foreach (var member in members) {
                    if (member is PropertyDeclarationSyntax propertyDecl) {

                        var sym = model.GetDeclaredSymbol(propertyDecl) as IPropertySymbol;
                        if (sym == null) throw new Exception($"Unable to get sym from {propertyDecl}");

                        var expr = propertyDecl.ExpressionBody?.Expression ?? propertyDecl.Initializer?.Value;
                        if (expr == null) {
                            throw new Exception($"unable to get intitializer value from {propertyDecl}");
                        }

                        infos.Add(new MemberInfo() {
                                sym = sym,
                                valueExpression = ValueExpression(expr),
                                makeNullable = makeNullable(sym.Type,propertyDecl.Type),
                                type = sym.Type,
                                name = sym.Name,
                                typeName = propertyDecl.Type.WithoutTrivia().ToFullString(),

                            });

                    } else if (member is FieldDeclarationSyntax fieldDecl) {
                        if (fieldDecl.Declaration.Variables.Count() != 1) throw new Exception($"Unsupported member, unsupported number of variables. {member.GetType()}");
                        var variable = fieldDecl.Declaration.Variables[0];

                        var sym = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (sym == null) throw new Exception($"Unable to get sym from {variable}");

                        // NOTE ignore backing fields already
                        if (variable.Initializer == null) continue;

                        // if error, make backing field
                        // TODO we have an attribute lookup
                        // if predefinedTypeSyntax -> make nullable, except string

                        infos.Add(new MemberInfo() {
                                sym = sym,
                                valueExpression = ValueExpression(variable.Initializer.Value),
                                makeNullable = makeNullable(sym.Type,fieldDecl.Declaration.Type),
                                type = sym.Type,
                                name = sym.Name,
                                typeName = fieldDecl.Declaration.Type.WithoutTrivia().ToFullString()
                            });


                        Console.WriteLine($"is {sym.Name} collection type? {types.IsCollectionType(sym.Type)}");

                    } else {
                        Console.WriteLine($"Warning Unsupported member {member.GetType()}");
                    }

                    static bool makeNullable(ITypeSymbol type, TypeSyntax typeSyntax) {
                        if (type.TypeKind == TypeKind.Error) {
                            Log.Warning($"cannot get type kind for {type.Name}, default to not make nullable");
                        }
                        if (type is INamedTypeSymbol namedType && namedType.IsGenericType) return false;
                        return type.TypeKind != TypeKind.Error && type.IsValueType;
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
            var newMembers = new List<MemberDeclarationSyntax>();
            foreach (var mem in memberInfos) {
                var typeSyntax = IdentifierName(mem.typeName);

                if (types.IsCollectionType(mem.type)) {

                    var defaultBackingFieldName = $"__{Utils.LowerFirstChar(mem.name)}Default__";
                    newMembers.Add(buildFieldDecl(_simpleType(mem),defaultBackingFieldName,SyntaxKind.StaticKeyword));

                    var defaultPropName = $"__{Utils.UpperFistChar(mem.name)}Default__";
                    newMembers.Add(buildProp(mem.typeName,defaultPropName,assignmentCoalesce(defaultBackingFieldName,mem.valueExpression)));

                    var adjBackingFieldName = $"__{mem.name}Adj__";
                    newMembers.Add(buildFieldDecl(_simpleType(mem),adjBackingFieldName,SyntaxKind.PublicKeyword));

                    var getExpr = coaleseExpr(adjBackingFieldName,defaultPropName);
                    var setExpr = AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(adjBackingFieldName),IdentifierName("value"));

                    newMembers.Add(buildProp(mem.typeName,mem.name,getExpr,setExpr));



                } else {

                    TypeSyntax newType = mem.makeNullable ?
                        nullableType(mem.typeName)
                        : IdentifierName(mem.typeName);

                    newMembers.Add(buildFieldDecl(newType,mem.name,SyntaxKind.PublicKeyword));


                }

            }

            static TypeSyntax _simpleType(MemberInfo mem) {
                return IdentifierName(mem.typeName);
            }

            return new SyntaxList<MemberDeclarationSyntax>(newMembers);
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
                // NOTE unkown tyes also return false for IsReferenceType

                if (types.IsCollectionType(mem.type)) {


                    newMembers.Add(buildProp(mem.typeName,mem.name,accessIExpression(adjClassName,mem.name)));


                } else if (mem.type.IsReferenceType) {

                    var backingFieldName = $"__{mem.name}__";
                    var backingField = buildFieldDecl(
                        IdentifierName(mem.typeName),
                        backingFieldName,
                        SyntaxKind.StaticKeyword);

                    var expr = coaleseExpr(
                        accessIExpression(adjClassName,mem.name),
                        // (__array__ ?? (__array__ = new []{1,2,3})
                        assignmentCoalesce(backingFieldName,mem.valueExpression));

                    newMembers.Add(backingField);
                    newMembers.Add(buildProp(mem.typeName,mem.name,expr));

                } else {

                    var expr = coaleseExpr(accessIExpression(adjClassName,mem.name),
                                           mem.valueExpression);

                    newMembers.Add(buildProp(mem.typeName,mem.name,expr));

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


        static PropertyDeclarationSyntax buildProp(string typeName, string name, ExpressionSyntax expr) {
            return blankProp(typeName,name)
                .WithExpressionBody(ArrowExpressionClause(expr))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        static PropertyDeclarationSyntax blankProp(string typeName, string name) {
            return PropertyDeclaration(
                IdentifierName(typeName),
                Identifier(name))
                .WithModifiers(modifierList(SyntaxKind.PublicKeyword,SyntaxKind.StaticKeyword));
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
                list = list.Add(Token(kind));
            }
            return list;
        }

        static TypeSyntax nullableType(string name) {
            return NullableType(IdentifierName(name));
        }

        static PropertyDeclarationSyntax buildProp(string typeName, string name, ExpressionSyntax getExpr, ExpressionSyntax setExpr) {
            return blankProp(typeName,name).WithAccessorList(
                AccessorList(
                    List<AccessorDeclarationSyntax>(
                        new AccessorDeclarationSyntax[] {
                            AccessorDeclaration(
                                SyntaxKind.GetAccessorDeclaration)
                            .WithExpressionBody(
                                ArrowExpressionClause(getExpr))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                            AccessorDeclaration(
                                SyntaxKind.SetAccessorDeclaration)
                            .WithExpressionBody(ArrowExpressionClause(setExpr))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                        }
                    )
                )
            ).NormalizeWhitespace("\n");

        }

        static ExpressionSyntax assignmentCoalesce(string leftName, ExpressionSyntax value) {
            return coaleseExpr(IdentifierName(leftName),
                        ParenthesizedExpression(AssignmentExpression(
                                                    SyntaxKind.SimpleAssignmentExpression,
                                                    IdentifierName(leftName),value)));

        }

        static ExpressionSyntax coaleseExpr(string left, string right) => coaleseExpr(IdentifierName(left),IdentifierName(right));

        static ExpressionSyntax coaleseExpr(ExpressionSyntax left, ExpressionSyntax right) {
            return BinaryExpression(SyntaxKind.CoalesceExpression,left,Token(SyntaxKind.QuestionQuestionToken),right);
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
