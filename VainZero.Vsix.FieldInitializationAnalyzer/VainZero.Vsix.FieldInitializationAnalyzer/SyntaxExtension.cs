using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VainZero.Vsix.FieldInitializationAnalyzer
{
    public static class SyntaxExtension
    {
        public static bool IsAutoImplemented(this PropertyDeclarationSyntax p)
        {
            if (p.AccessorList == null) return false;

            return p.AccessorList.Accessors.All(a => a != null && a.Body == null)
                && p.Initializer == null
                && !p.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));
        }

        public static bool IsGetOnlyAutoImplemented(this PropertyDeclarationSyntax p)
        {
            if (p.AccessorList == null) return false;
            if (p.AccessorList.Accessors.Count != 1) return false;

            var accessor = p.AccessorList.Accessors[0];

            return accessor != null
                && accessor.Keyword.IsKind(SyntaxKind.GetKeyword)
                && accessor.Body == null
                && p.Initializer == null
                && !p.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));
        }
    }
}
