using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VainZero.FieldInitializationAnalyzer
{
    public static class SyntaxExtension
    {
        /// <summary>
        /// Gets a value indicating whether the node is assigned,
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
