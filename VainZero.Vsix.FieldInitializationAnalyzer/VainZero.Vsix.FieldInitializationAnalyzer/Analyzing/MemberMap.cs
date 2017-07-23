using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VainZero.Vsix.FieldInitializationAnalyzer.Analyzing
{
    public sealed class MemberMap
    {
        public ImmutableDictionary<ISymbol, MemberVariable> MemberVariables { get; }

        public ImmutableDictionary<ISymbol, MethodDeclarationSyntax> Methods { get; }

        public ImmutableDictionary<ISymbol, AccessorDeclarationSyntax> Setters { get; }

        public ImmutableArray<KeyValuePair<ISymbol, AccessorDeclarationSyntax>> PublicSetters { get; }

        public ImmutableArray<KeyValuePair<ISymbol, ConstructorDeclarationSyntax>> Constructors { get; }

        public ImmutableHashSet<ISymbol> DelegatedConstructors { get; }

        public MemberMap(ImmutableDictionary<ISymbol, MemberVariable> memberVariables,
                ImmutableDictionary<ISymbol, MethodDeclarationSyntax> methods,
                ImmutableDictionary<ISymbol, AccessorDeclarationSyntax> setters,
                ImmutableArray<KeyValuePair<ISymbol, AccessorDeclarationSyntax>> publicSetters,
                ImmutableArray<KeyValuePair<ISymbol, ConstructorDeclarationSyntax>> constructors,
                ImmutableHashSet<ISymbol> delegatedConstructors)
        {
            MemberVariables = memberVariables;
            Methods = methods;
            Setters = setters;
            PublicSetters = publicSetters;
            Constructors = constructors;
            DelegatedConstructors = delegatedConstructors;
        }
    }
}
