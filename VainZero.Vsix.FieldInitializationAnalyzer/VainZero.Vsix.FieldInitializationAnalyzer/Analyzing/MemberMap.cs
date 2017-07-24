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
        public ImmutableDictionary<ISymbol, MemberVariable> Variables { get; }

        public ImmutableDictionary<ISymbol, MethodDeclarationSyntax> Methods { get; }

        public ImmutableDictionary<ISymbol, Property> Properties { get; }

        public ImmutableArray<KeyValuePair<ISymbol, ConstructorDeclarationSyntax>> Constructors { get; }

        public ImmutableHashSet<ISymbol> DelegatedConstructors { get; }

        public MemberMap(ImmutableDictionary<ISymbol, MemberVariable> variables,
                ImmutableDictionary<ISymbol, MethodDeclarationSyntax> methods,
                ImmutableDictionary<ISymbol, Property> properties,
                ImmutableArray<KeyValuePair<ISymbol, ConstructorDeclarationSyntax>> constructors,
                ImmutableHashSet<ISymbol> delegatedConstructors)
        {
            Variables = variables;
            Methods = methods;
            Properties = properties;
            Constructors = constructors;
            DelegatedConstructors = delegatedConstructors;
        }
    }
}
