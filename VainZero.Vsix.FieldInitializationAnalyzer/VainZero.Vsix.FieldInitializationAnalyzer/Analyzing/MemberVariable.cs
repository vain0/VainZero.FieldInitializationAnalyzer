using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace VainZero.Vsix.FieldInitializationAnalyzer.Analyzing
{
    /// <summary>
    /// Represents a field or auto-implemented property.
    /// </summary>
    public sealed class MemberVariable
    {
        public ISymbol Symbol { get; }

        /// <summary>
        /// Gets a value indicating whether the variable can be uninitialized,
        /// i.e. the variable provides a public method to update.
        /// </summary>
        public bool CanBeUninitialized { get; }

        public MemberVariable(ISymbol symbol, bool canBeUninitialized)
        {
            Symbol = symbol;
            CanBeUninitialized = canBeUninitialized;
        }
    }
}
