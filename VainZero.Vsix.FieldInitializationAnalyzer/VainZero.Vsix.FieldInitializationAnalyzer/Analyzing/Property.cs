using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VainZero.Vsix.FieldInitializationAnalyzer.Analyzing
{
    public sealed class Property
    {
        public IPropertySymbol Symbol { get; }
        public AccessorDeclarationSyntax GetterDecl { get; }
        public AccessorDeclarationSyntax SetterDecl { get; }

        public Property(IPropertySymbol symbol, AccessorDeclarationSyntax getterDecl, AccessorDeclarationSyntax setterDecl)
        {
            Symbol = symbol;
            GetterDecl = getterDecl;
            SetterDecl = setterDecl;
        }
    }
}
