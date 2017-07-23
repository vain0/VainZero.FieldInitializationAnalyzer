using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VainZero.Vsix.FieldInitializationAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class VainZeroVsixFieldInitializationAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            Analyzing.MyReporter.SupportedDiagnostics();

        static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            var typeDecl = (TypeDeclarationSyntax)context.Node;
            new Analyzing.MyTypeAnalyzer(context).Analyze(typeDecl);
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.ClassDeclaration);
        }
    }
}
