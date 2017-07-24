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

        /// <summary>
        /// Gets a value indicating whether the expression is being assigned,
        /// i.e. appears on the left hand side of assignment expressions
        /// or as the operand of increment/decrement operators.
        /// </summary>
        public static bool IsAssigned(this SyntaxNode node)
        {
            while (true)
            {
                if (node == null) return false;

                var parent = node.Parent;
                if (parent == null) return false;

                switch (parent)
                {
                    case AssignmentExpressionSyntax assignment:
                        return node == assignment.Left;

                    case PrefixUnaryExpressionSyntax prefix:
                        return
                            prefix.IsKind(SyntaxKind.PreIncrementExpression)
                            && prefix.Operand == node;

                    case PostfixUnaryExpressionSyntax postfix:
                        return
                            postfix.IsKind(SyntaxKind.PostIncrementExpression)
                            && postfix.Operand == node;

                    case ElementAccessExpressionSyntax elementAccess:
                        return elementAccess.Expression == node;

                    case MemberAccessExpressionSyntax memberAccess:
                        if (node == memberAccess.Expression) return false;
                        break;

                    case ParenthesizedExpressionSyntax _:
                        break;

                    default:
                        return false;
                }

                node = parent;
            }
        }
    }
}
