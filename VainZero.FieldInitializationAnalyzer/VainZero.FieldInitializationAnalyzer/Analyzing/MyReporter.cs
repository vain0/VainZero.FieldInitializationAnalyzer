using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VainZero.FieldInitializationAnalyzer
{
    public sealed class MyReporter
    {
        #region Rules
        const string ConstructorDiagnosticId = "VainZeroFieldInitializationAnalyzerConstructorDiagnostic";
        const string FieldDiagnosticId = "VainZeroFieldInitializationAnalyzerFieldDiagnostic";

        const string Category = "Code Analysis";

        static DiagnosticDescriptor ConstructorRule { get; } =
            new DiagnosticDescriptor(
                ConstructorDiagnosticId,
                "Constructor not initializating fields",
                "The constructor doesn't initialize: {0}.",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "Constructors should initialize all fields."
            );

        static DiagnosticDescriptor FieldRule { get; } =
            new DiagnosticDescriptor(
                FieldDiagnosticId,
                "Uninitialized field or property",
                "The field or property '{0}' is used before initialization.",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "Fields should be initialized before use."
            );

        public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(ConstructorRule, FieldRule);
        #endregion

        public SyntaxNodeAnalysisContext AnalysisContext { get; }

        public MyReporter(SyntaxNodeAnalysisContext analysisContext)
        {
            AnalysisContext = analysisContext;
        }

        public void ReportConstructorDiagnostic(Location location, ImmutableArray<ISymbol> uninitializedSymbols)
        {
            var diagnostic =
                Diagnostic.Create(
                    ConstructorRule,
                    location,
                    string.Join(", ", uninitializedSymbols.Select(s => $"'{s.Name}'"))
                );
            AnalysisContext.ReportDiagnostic(diagnostic);
        }

        public void ReportFieldDiagnostic(Location location, ISymbol symbol)
        {
            var diagnostic = Diagnostic.Create(FieldRule, location, symbol.Name);
            AnalysisContext.ReportDiagnostic(diagnostic);
        }
    }
}
