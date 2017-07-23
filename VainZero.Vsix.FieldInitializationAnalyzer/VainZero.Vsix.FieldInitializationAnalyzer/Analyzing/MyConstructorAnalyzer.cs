using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VainZero.Vsix.FieldInitializationAnalyzer.Analyzing
{
    public sealed class MyConstructorAnalyzer
    {
        SyntaxNodeAnalysisContext AnalysisContext { get; }

        SemanticModel SemanticModel => AnalysisContext.SemanticModel;

        MyReporter Reporter { get; }

        MemberMap MemberMap { get; }

        public MyConstructorAnalyzer(SyntaxNodeAnalysisContext analysisContext,
                MemberMap memberMap)
        {
            AnalysisContext = analysisContext;
            MemberMap = memberMap;

            Reporter = new MyReporter(analysisContext);
        }

        bool enablesFieldDiagnostic = true;

        #region VisitedSymbols
        HashSet<ISymbol> VisitedSymbols { get; } = new HashSet<ISymbol>();

        /// <summary>
        /// Tries to visit the body of a symbol (constructor, method or setter).
        /// Returns <c>false</c> if visited or currently visiting.
        /// </summary>
        public bool TryVisit(ISymbol symbol)
        {
            if (!VisitedSymbols.Add(symbol)) return false;

            // In case of infinite loop.
            if (VisitedSymbols.Count > 100)
            {
                System.Diagnostics.Debug.WriteLine("An infinite loop is detected.");
                return false;
            }

            return true;
        }
        #endregion

        #region InitializedSymbols
        HashSet<ISymbol> InitializedSymbols { get; } =
            new HashSet<ISymbol>();

        bool IsMemberVariable(ISymbol symbol)
        {
            return MemberMap.MemberVariables.ContainsKey(symbol);
        }

        bool IsInitialized(ISymbol symbol)
        {
            return InitializedSymbols.Contains(symbol);
        }

        void MarkAsInitialized(ISymbol symbol)
        {
            if (IsMemberVariable(symbol))
            {
                InitializedSymbols.Add(symbol);
            }
        }
        #endregion

        void MarkOutArgumentsAsInitialized(ArgumentListSyntax argumentList)
        {
            if (argumentList == null || argumentList.Arguments == null) return;

            foreach (var argument in argumentList.Arguments)
            {
                if (argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword)
                    || argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword))
                {
                    if (argument.Expression == null) continue;

                    var symbol = SemanticModel.GetSymbolInfo(argument.Expression).Symbol;
                    if (symbol == null) continue;

                    MarkAsInitialized(symbol);
                }
            }
        }

        void AnalyzeInvocation(InvocationExpressionSyntax invocation)
        {
            var symbol = SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;
            if (symbol != null
                && MemberMap.Methods.TryGetValue(symbol, out var methodDecl)
                && methodDecl.Body != null
                )
            {
                AnalyzeMethod(methodDecl.Body, symbol);
            }
        }

        void AnalyzeAssignment(AssignmentExpressionSyntax assignment)
        {
            // An expression ``left = ...`` initializes `left`.

            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)) return;
            if (assignment.Left == null) return;

            var symbol = SemanticModel.GetSymbolInfo(assignment.Left).Symbol;
            if (symbol == null) return;

            MarkAsInitialized(symbol);

            if (MemberMap.Setters.TryGetValue(symbol, out var accessorDecl) && accessorDecl.Body != null)
            {
                AnalyzeMethod(accessorDecl.Body, symbol);
            }
        }

        static bool IsAssigned(SyntaxNode node)
        {
            while (true)
            {
                if (node == null) return false;

                var parent = node.Parent;
                if (parent == null) return false;

                if (parent is AssignmentExpressionSyntax assignment && assignment.Left == node) return true;

                node = parent;
            }
        }

        void AnalyzeIdentifier(IdentifierNameSyntax identifier)
        {
            var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol == null) return;

            if (IsAssigned(identifier))
            {

            }

            if (IsMemberVariable(symbol) && !IsInitialized(symbol))
            {
                Reporter.ReportFieldDiagnostic(identifier.GetLocation(), symbol);
            }

        }

        void AnalyzeStatements(SyntaxList<StatementSyntax> statements)
        {
            foreach (var statement in statements)
            {
                foreach (var node in statement.DescendantNodes())
                {
                    if (node is InvocationExpressionSyntax invocation)
                    {
                        MarkOutArgumentsAsInitialized(invocation.ArgumentList);
                        AnalyzeInvocation(invocation);
                    }
                    else if (node is AssignmentExpressionSyntax assignment)
                    {
                        AnalyzeAssignment(assignment);
                    }
                    else if (node is IdentifierNameSyntax identifier)
                    {
                        AnalyzeIdentifier(identifier);
                    }
                }
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
                AnalyzeConstructor(delegateConstructorDecl, symbol);
            }
        }

        void AnalyzeConstructor(ConstructorDeclarationSyntax constructorDecl, ISymbol symbol)
        {
            if (!TryVisit(symbol)) return;

            AnalyzeDelegateConstructor(constructorDecl);

            var body = constructorDecl.Body;
            if (body != null)
            {
                AnalyzeStatements(body.Statements);
            }
        }

        void AnalyzePublicSetters()
        {
            foreach (var kv in MemberMap.PublicSetters)
            {
                if (kv.Value.Body == null) continue;
                AnalyzeMethod(kv.Value.Body, kv.Key);
            }
        }

        /// <summary>
        /// Reports a warning if any field isn't initialized with
        /// the initializer, constructor or public setters.
        /// </summary>
        void ReportUninitializedFields(ConstructorDeclarationSyntax constructorDecl)
        {
            var uninitializedSymbols =
                MemberMap.MemberVariables
                .Where(kv => !IsInitialized(kv.Key) && !kv.Value.CanBeUninitialized)
                .Select(kv => kv.Key)
                .ToImmutableArray();

            if (uninitializedSymbols.Length != 0)
            {
                Reporter.ReportConstructorDiagnostic(constructorDecl.GetLocation(), uninitializedSymbols);
            }
        }

        public void Analyze(ConstructorDeclarationSyntax constructorDecl, ISymbol constructorSymbol)
        {
            AnalyzeConstructor(constructorDecl, constructorSymbol);
            AnalyzePublicSetters();
            ReportUninitializedFields(constructorDecl);
        }
    }
}
