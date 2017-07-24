using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VainZero.FieldInitializationAnalyzer
{
    public sealed class MemberMapCreator
    {
        SemanticModel SemanticModel { get; }

        ImmutableDictionary<ISymbol, MemberVariable>.Builder Variables { get; } =
            ImmutableDictionary.CreateBuilder<ISymbol, MemberVariable>();

        ImmutableDictionary<ISymbol, MethodDeclarationSyntax>.Builder Methods { get; } =
            ImmutableDictionary.CreateBuilder<ISymbol, MethodDeclarationSyntax>();

        ImmutableDictionary<ISymbol, Property>.Builder Properties { get; } =
            ImmutableDictionary.CreateBuilder<ISymbol, Property>();

        ImmutableArray<KeyValuePair<ISymbol, ConstructorDeclarationSyntax>>.Builder Constructors { get; } =
            ImmutableArray.CreateBuilder<KeyValuePair<ISymbol, ConstructorDeclarationSyntax>>();

        ImmutableHashSet<ISymbol>.Builder DelegatedConstructors { get; } =
            ImmutableHashSet.CreateBuilder<ISymbol>();

        public MemberMapCreator(SemanticModel semanticModel)
        {
            SemanticModel = semanticModel;
        }

        void AddField(FieldDeclarationSyntax fieldDecl)
        {
            if (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) return;

            foreach (var varDecl in fieldDecl.Declaration.Variables)
            {
                if (varDecl.Initializer != null) continue;

                var symbol = SemanticModel.GetDeclaredSymbol(varDecl);
                if (symbol == null) continue;

                var isReadOnly = fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
                var isPublic = symbol.DeclaredAccessibility == Accessibility.Public;
                var canBeUninitialized = !isReadOnly && isPublic;

                Variables.Add(symbol, new MemberVariable(symbol, canBeUninitialized));
            }
        }

        void AddPropertyAsMemberVariable(PropertyDeclarationSyntax propertyDecl, IPropertySymbol symbol)
        {
            if (propertyDecl.ExpressionBody != null) return;
            if (propertyDecl.Initializer != null) return;

            if (propertyDecl.AccessorList == null) return;
            if (propertyDecl.AccessorList.Accessors.Any(a => a.Body != null)) return;

            if (symbol.IsAbstract || symbol.IsStatic || symbol.IsIndexer) return;

            var hasNonprivateSetter =
                symbol.SetMethod != null
                && symbol.SetMethod.DeclaredAccessibility != Accessibility.Private;

            Variables.Add(symbol, new MemberVariable(symbol, canBeUninitialized: hasNonprivateSetter));
        }

        void AddPropertyOrIndexer(BasePropertyDeclarationSyntax propertyDecl, IPropertySymbol symbol)
        {
            if (symbol.IsAbstract || symbol.IsStatic) return;
            if (propertyDecl.AccessorList == null) return;

            var getterDecl = default(AccessorDeclarationSyntax);
            var setterDecl = default(AccessorDeclarationSyntax);

            foreach (var accessor in propertyDecl.AccessorList.Accessors)
            {
                if (accessor.Keyword.IsKind(SyntaxKind.GetKeyword))
                {
                    getterDecl = accessor;
                }
                else if (accessor.Keyword.IsKind(SyntaxKind.SetKeyword))
                {
                    setterDecl = accessor;
                }
            }

            Properties.Add(symbol, new Property(symbol, getterDecl, setterDecl));
        }

        void AddMethod(MethodDeclarationSyntax methodDecl)
        {
            if (methodDecl.Body == null) return;

            var symbol = SemanticModel.GetDeclaredSymbol(methodDecl);
            if (symbol == null) return;

            Methods.Add(symbol, methodDecl);
        }

        bool TryGetDelegatedConstructor(ConstructorDeclarationSyntax sourceDecl, out ISymbol delegatedConstructorSymbol)
        {
            var initializer = sourceDecl.Initializer;
            if (initializer != null && initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword))
            {
                var symbol = SemanticModel.GetSymbolInfo(initializer).Symbol;
                if (symbol != null)
                {
                    delegatedConstructorSymbol = symbol;
                    return true;
                }
            }

            delegatedConstructorSymbol = default(ISymbol);
            return false;
        }

        void AddConstrcutor(ConstructorDeclarationSyntax constructorDecl)
        {
            var symbol = SemanticModel.GetDeclaredSymbol(constructorDecl);
            if (symbol != null)
            {
                Constructors.Add(new KeyValuePair<ISymbol, ConstructorDeclarationSyntax>(symbol, constructorDecl));
            }

            if (TryGetDelegatedConstructor(constructorDecl, out var delegatedConstructorSymbol))
            {
                DelegatedConstructors.Add(delegatedConstructorSymbol);
            }
        }

        public MemberMap Create(TypeDeclarationSyntax typeDecl)
        {
            foreach (var member in typeDecl.Members)
            {
                if (member is FieldDeclarationSyntax fieldDecl)
                {
                    AddField(fieldDecl);
                }
                else if (member is PropertyDeclarationSyntax propertyDecl)
                {
                    var symbol = SemanticModel.GetDeclaredSymbol(propertyDecl);
                    if (symbol == null) continue;

                    AddPropertyAsMemberVariable(propertyDecl, symbol);
                    AddPropertyOrIndexer(propertyDecl, symbol);
                }
                else if (member is IndexerDeclarationSyntax indexerDecl)
                {
                    var symbol = SemanticModel.GetDeclaredSymbol(indexerDecl);
                    if (symbol == null) continue;

                    AddPropertyOrIndexer(indexerDecl, symbol);
                }
                else if (member is MethodDeclarationSyntax methodDecl)
                {
                    AddMethod(methodDecl);
                }
                else if (member is ConstructorDeclarationSyntax constructorDecl)
                {
                    AddConstrcutor(constructorDecl);
                }
            }

            return
                new MemberMap(
                    Variables.ToImmutable(),
                    Methods.ToImmutable(),
                    Properties.ToImmutable(),
                    Constructors.ToImmutable(),
                    DelegatedConstructors.ToImmutable()
                );
        }
    }
}
