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

            ImmutableDictionary<ISymbol, BlockSyntax> Methods { get; }

            /// <summary>
            /// A set of symbols (constructor/method/property) whose body is analyzing or analyzed.
            /// </summary>
            HashSet<ISymbol> VisitedSymbols { get; } = new HashSet<ISymbol>();

            public Diagnoser(SyntaxNodeAnalysisContext analysisContext, ImmutableDictionary<ISymbol, Member> members, ImmutableDictionary<ISymbol, BlockSyntax> methods)
            {
                AnalysisContext = analysisContext;
                Members = members;
                Methods = methods;
            }

            bool TryVisit(ISymbol symbol)
            {
                if (!VisitedSymbols.Add(symbol)) return false;

                // In case of infinite loop...
                if (VisitedSymbols.Count > 100)
                {
                    System.Diagnostics.Debug.WriteLine("An infinite loop is detected.");
                    return false;
                }

                return true;
            }

            void MarkAsInitialized(ISymbol symbol)
            {
                if (Members.TryGetValue(symbol, out var member))
                {
                    member.IsInitialized = true;
                }
            }

            void MarkOutArgumentsAsInitialized(ArgumentListSyntax argumentList)
            {
                if (argumentList == null || argumentList.Arguments == null) return;

                foreach (var argument in argumentList.Arguments)
                {
                    if (argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
                    {
                        if (argument.Expression == null) continue;

                        var symbol = SemanticModel.GetSymbolInfo(argument.Expression).Symbol;
                        if (symbol == null) continue;

                        MarkAsInitialized(symbol);
                    }
                }
            }

            void AnalyzeStatement(StatementSyntax statement)
            {
                foreach (var node in statement.DescendantNodes())
                {
                    if (node is InvocationExpressionSyntax invocation)
                    {
                        MarkOutArgumentsAsInitialized(invocation.ArgumentList);

                        var symbol = SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;
                        if (symbol != null && Methods.TryGetValue(symbol, out var block))
                        {
                            AnalyzeMethod(block, symbol);
                        }
                    }
                    else if (node is AssignmentExpressionSyntax assignment
                        && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                        && assignment.Left != null)
                    {
                        // An expression ``left = ...`` initializes `left`.

                        var symbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
                        if (symbol == null) continue;

                        MarkAsInitialized(symbol);

                        if (Methods.TryGetValue(symbol, out var block))
                        {
                            AnalyzeMethod(block, symbol);
                        }
                    }
                    else if (node is IdentifierNameSyntax identifier)
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

            void AnalyzeMethod(BlockSyntax block, ISymbol symbol)
            {
                if (!TryVisit(symbol)) return;

                AnalyzeStatements(block.Statements);
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

            ImmutableDictionary<ISymbol, BlockSyntax>.Builder Methods { get; } =
                ImmutableDictionary.CreateBuilder<ISymbol, BlockSyntax>();

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

            void AddPropertyAsMember(PropertyDeclarationSyntax propertyDecl, IPropertySymbol symbol)
            {
                if (propertyDecl.ExpressionBody != null) return;
                if (propertyDecl.Initializer != null) return;

                if (propertyDecl.AccessorList == null) return;
                if (propertyDecl.AccessorList.Accessors.Any(a => a.Body != null)) return;

                if (symbol.IsAbstract || symbol.IsStatic) return;

                var hasNonprivateSetter =
                    !symbol.IsReadOnly
                    && symbol.SetMethod.DeclaredAccessibility != Accessibility.Private;

                Members.Add(symbol, new Member(symbol, canBeUninitialized: hasNonprivateSetter));
            }

            void AddPropertyAsMethod(PropertyDeclarationSyntax propertyDecl, IPropertySymbol symbol)
            {
                if (symbol.IsReadOnly || symbol.IsAbstract || symbol.IsStatic) return;

                if (propertyDecl.AccessorList == null) return;
                var setter = 
                    propertyDecl.AccessorList.Accessors
                    .FirstOrDefault(a => a.Keyword.IsKind(SyntaxKind.SetKeyword));
                if (setter == null || setter.Body == null) return;

                Methods.Add(symbol, setter.Body);
            }

            void AddMethod(MethodDeclarationSyntax methodDecl)
            {
                if (methodDecl.Body == null) return;

                var symbol = SemanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol == null) return;

                Methods.Add(symbol, methodDecl.Body);
            }

            public Diagnoser Create(TypeDeclarationSyntax typeDecl)
            {
                foreach (var member in typeDecl.Members)
                {
                    if (member is FieldDeclarationSyntax fieldDecl)
                    {
                        AddField(fieldDecl);
                    }
                    else if (member is PropertyDeclarationSyntax propertyDecl)
                    {
                        var symbol = SemanticModel.GetDeclaredSymbol(propertyDecl);
                        if (symbol == null) continue;

                        AddPropertyAsMember(propertyDecl, symbol);
                        AddPropertyAsMethod(propertyDecl, symbol);
                    }
                    else if (member is MethodDeclarationSyntax methodDecl)
                    {
                        AddMethod(methodDecl);
                    }
                }

                return new Diagnoser(AnalysisContext, Members.ToImmutable(), Methods.ToImmutable());
            }
        }

        sealed class ConstructorDelegationDetector
        {
            SemanticModel SemanticModel { get; }

            public ConstructorDelegationDetector(SemanticModel semanticModel)
            {
                SemanticModel = semanticModel;
            }

            /// <summary>
            /// Gets a value indicating whether the specified constructor is delegated by other constructors.
            /// </summary>
            public bool IsDelegated(ConstructorDeclarationSyntax targetDecl, TypeDeclarationSyntax typeDecl)
            {
                var target = SemanticModel.GetDeclaredSymbol(targetDecl);
                if (target == null) return false;

                foreach (var member in typeDecl.Members)
                {
                    if (member is ConstructorDeclarationSyntax sourceDecl)
                    {
                        var initializer = sourceDecl.Initializer;
                        if (initializer == null) continue;
                        if (!initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword)) continue;

                        var symbol = SemanticModel.GetDeclaredSymbol(initializer);
                        if (symbol == target) return true;
                    }
                }

                return false;
            }
        }

        static bool IsPrivate(ConstructorDeclarationSyntax constructorDecl)
        {
            foreach (var modifier in constructorDecl.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PublicKeyword)
                    || modifier.IsKind(SyntaxKind.InternalKeyword)
                    || modifier.IsKind(SyntaxKind.ProtectedKeyword)
                    ) return false;
            }
            return true;
        }

        /// <summary>
        /// Analyzes a constructor to ensure it initializes all fields including auto-implemented properties.
        /// </summary>
        static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            var constructorDecl = (ConstructorDeclarationSyntax)context.Node;
            var typeDecl = (TypeDeclarationSyntax)constructorDecl.Parent;

            // If the constructor is private and delegated by other constructors,
            // it doesn't need to initialize all fields
            // because delegating constructors do.
            if (IsPrivate(constructorDecl)
                && new ConstructorDelegationDetector(context.SemanticModel).IsDelegated(constructorDecl, typeDecl)) return;

            var diagnoser = new DiagnoserCreator(context).Create(typeDecl);
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
