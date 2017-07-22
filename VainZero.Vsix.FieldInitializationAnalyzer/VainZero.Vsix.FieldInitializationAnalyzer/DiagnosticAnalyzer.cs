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
        public const string ConstructorDiagnosticId = "VainZeroFieldInitializationAnalyzerConstructorDiagnostic";
        public const string FieldDiagnosticId = "VainZeroFieldInitializationAnalyzerFieldDiagnostic";

        const string Category = "Code Analysis";

        static readonly DiagnosticDescriptor ConstructorRule =
            new DiagnosticDescriptor(
                ConstructorDiagnosticId,
                "Constructor not initializating fields",
                "The constructor doesn't initialize: {0}.",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "Constructors should initialize all fields."
            );

        static readonly DiagnosticDescriptor FieldRule =
            new DiagnosticDescriptor(
                FieldDiagnosticId,
                "Uninitialized field or property",
                "The field or property '{0}' is used before initialization.",
                Category,
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true,
                description: "Fields should be initialized before use."
            );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(ConstructorRule, FieldRule);

        public override void Initialize(AnalysisContext context)
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information

            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        }

        /// <summary>
        /// Represents a field or auto-implemented property without initializers.
        /// </summary>
        sealed class Member
        {
            public ISymbol Symbol { get; }
            public bool CanBeUninitialized { get; }
            public bool IsInitialized { get; set; } = false;
            public bool ErrorReported { get; set; } = false;

            public Member(ISymbol symbol, bool canBeUninitialized)
            {
                Symbol = symbol;
                CanBeUninitialized = canBeUninitialized;
            }
        }

        sealed class Diagnoser
        {
            SyntaxNodeAnalysisContext AnalysisContext { get; }

            SemanticModel SemanticModel => AnalysisContext.SemanticModel;

            ImmutableDictionary<ISymbol, Member> Members { get; }

            HashSet<ISymbol> Done { get; } = new HashSet<ISymbol>();

            public Diagnoser(SyntaxNodeAnalysisContext analysisContext, ImmutableDictionary<ISymbol, Member> members)
            {
                AnalysisContext = analysisContext;
                Members = members;
            }

            bool TryVisit(ISymbol symbol)
            {
                if (!Done.Add(symbol)) return false;

                // In case of infinite loop...
                if (Done.Count > 1000)
                {
                    System.Diagnostics.Debug.WriteLine("An infinite loop is detected.");
                    return false;
                }

                return true;
            }

            static bool TryAssignedExpression(SyntaxNode node, out ExpressionSyntax expression)
            {
                if (node is AssignmentExpressionSyntax assignment
                    && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && assignment.Left != null)
                {
                    expression = assignment.Left;
                    return true;
                }
                else if (node is ArgumentSyntax argument && argument.Expression != null)
                {
                    expression = argument.Expression;
                    return true;
                }

                expression = default(ExpressionSyntax);
                return false;
            }

            void AnalyzeStatement(StatementSyntax statement)
            {
                foreach (var node in statement.DescendantNodes())
                {
                    if (TryAssignedExpression(node, out var expression))
                    {
                        var symbol = SemanticModel.GetSymbolInfo(expression).Symbol;
                        if (symbol == null) continue;

                        if (Members.TryGetValue(symbol, out var member))
                        {
                            member.IsInitialized = true;
                        }
                    }

                    if (node is IdentifierNameSyntax identifier)
                    {
                        var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
                        if (symbol == null) continue;

                        if (Members.TryGetValue(symbol, out var member) && !member.IsInitialized)
                        {
                            AnalysisContext.ReportDiagnostic(Diagnostic.Create(FieldRule, identifier.GetLocation(), symbol.Name));
                            member.ErrorReported = true;
                        }
                    }
                }
            }

            void AnalyzeStatements(SyntaxList<StatementSyntax> statements)
            {
                foreach (var statement in statements)
                {
                    AnalyzeStatement(statement);
                }
            }

            void AnalyzeDelegateConstructor(ConstructorDeclarationSyntax constructorDecl)
            {
                var initializer = constructorDecl.Initializer;
                if (initializer == null) return;
                if (!initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword)) return;

                var symbol = SemanticModel.GetSymbolInfo(initializer).Symbol;
                if (symbol == null || symbol.DeclaringSyntaxReferences.Length != 1) return;

                var syntaxRef = symbol.DeclaringSyntaxReferences[0];
                var node = syntaxRef.GetSyntax();

                if (node is ConstructorDeclarationSyntax delegateConstructorDecl)
                {
                    AnalyzeConstructor(delegateConstructorDecl);
                }
            }

            void AnalyzeConstructor(ConstructorDeclarationSyntax constructorDecl)
            {
                var symbol = SemanticModel.GetDeclaredSymbol(constructorDecl);
                if (symbol == null) return;
                if (!TryVisit(symbol)) return;

                AnalyzeDelegateConstructor(constructorDecl);

                var body = constructorDecl.Body;
                if (body != null)
                {
                    AnalyzeStatements(body.Statements);
                }
            }

            public void Analyze(ConstructorDeclarationSyntax constructorDecl)
            {
                AnalyzeConstructor(constructorDecl);

                var uninitializedUnusedMembers =
                    Members
                    .Where(kv =>
                        !kv.Value.IsInitialized
                        && !kv.Value.ErrorReported
                        && !kv.Value.CanBeUninitialized
                    )
                    .Select(kv => kv.Value)
                    .ToImmutableArray();
                if (uninitializedUnusedMembers.Length != 0)
                {
                    AnalysisContext.ReportDiagnostic(
                        Diagnostic.Create(
                            ConstructorRule,
                            constructorDecl.GetLocation(),
                            string.Join(", ", uninitializedUnusedMembers.Select(m => m.Symbol.Name))
                        ));
                }
            }
        }

        sealed class DiagnoserCreator
        {
            SyntaxNodeAnalysisContext AnalysisContext { get; }

            SemanticModel SemanticModel => AnalysisContext.SemanticModel;

            ImmutableDictionary<ISymbol, Member>.Builder Members { get; } =
                ImmutableDictionary.CreateBuilder<ISymbol, Member>();

            public DiagnoserCreator(SyntaxNodeAnalysisContext analysisContext)
            {
                AnalysisContext = analysisContext;
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

                    Members.Add(symbol, new Member(symbol, canBeUninitialized));
                }
            }

            void AddProperty(PropertyDeclarationSyntax propertyDecl)
            {
                if (propertyDecl.ExpressionBody != null) return;
                if (propertyDecl.Initializer != null) return;

                if (propertyDecl.Modifiers.Any(m =>
                    m.IsKind(SyntaxKind.AbstractKeyword)
                    || m.IsKind(SyntaxKind.StaticKeyword)
                    )) return;

                if (propertyDecl.AccessorList?.Accessors.Any(a => a.Body != null) != false) return;

                var symbol = SemanticModel.GetDeclaredSymbol(propertyDecl);
                if (symbol == null) return;

                var hasNonprivateSetter =
                    !symbol.IsReadOnly
                    && symbol.SetMethod.DeclaredAccessibility != Accessibility.Private;

                Members.Add(symbol, new Member(symbol, canBeUninitialized: hasNonprivateSetter));
            }

            public Diagnoser Create(ConstructorDeclarationSyntax constructorDecl)
            {
                var typeDecl = (TypeDeclarationSyntax)constructorDecl.Parent;

                foreach (var member in typeDecl.Members)
                {
                    if (member is FieldDeclarationSyntax fieldDecl)
                    {
                        AddField(fieldDecl);
                    }
                    else if (member is PropertyDeclarationSyntax propertyDecl)
                    {
                        AddProperty(propertyDecl);
                    }
                }

                return new Diagnoser(AnalysisContext, Members.ToImmutable());
            }
        }

        /// <summary>
        /// Analyzes a constructor to ensure it initializes all fields including auto-implemented properties.
        /// </summary>
        static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            // このコンストラクターが private で、直接 new する式がないなら、解析しない。

            var constructorDecl = (ConstructorDeclarationSyntax)context.Node;
            var diagnoser = new DiagnoserCreator(context).Create(constructorDecl);
            diagnoser.Analyze(constructorDecl);
        }

        /*
        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
        */
    }
}
