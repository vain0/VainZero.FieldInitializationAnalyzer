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

namespace VainZero.FieldInitializationAnalyzer
{
    public sealed class MyCodeAnalyzer
    {
        SyntaxNodeAnalysisContext AnalysisContext { get; }

        SemanticModel SemanticModel => AnalysisContext.SemanticModel;

        MyReporter Reporter { get; }

        MemberMap MemberMap { get; }

        public MyCodeAnalyzer(SyntaxNodeAnalysisContext analysisContext,
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
        /// Returns <c>false</c> if visited, currently visiting or maybe in an infinite loop.
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
            return MemberMap.Variables.ContainsKey(symbol);
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
        }

        void AnalyzeProperty(Property property, bool isAssigned)
        {
            var body = isAssigned ? property.SetterDecl?.Body : property.GetterDecl?.Body;
            if (body != null)
            {
                AnalyzeStatements(body.Statements);
            }
        }

        void AnalyzeIdentifier(IdentifierNameSyntax identifier)
        {
            var symbol = SemanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol == null) return;

            if (enablesFieldDiagnostic
                && IsMemberVariable(symbol)
                && !IsInitialized(symbol)
                )
            {
                Reporter.ReportFieldDiagnostic(identifier.GetLocation(), symbol);
            }

            if (MemberMap.Properties.TryGetValue(symbol, out var property))
            {
                AnalyzeProperty(property, identifier.IsAssigned());
            }
        }

        void AnalyzeThis(ThisExpressionSyntax thisExpression)
        {
            // Indexer case.
            if (thisExpression.Parent is ElementAccessExpressionSyntax elementAccess
                && elementAccess.Expression == thisExpression)
            {
                var indexer = MemberMap.Properties.Values.FirstOrDefault(p => p.Symbol.IsIndexer);
                if (indexer != null)
                {
                    AnalyzeProperty(indexer, elementAccess.IsAssigned());
                }
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
                    else if (node is ThisExpressionSyntax thisExpression)
                    {
                        AnalyzeThis(thisExpression);
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

        void AnalyzeNonprivateSetters()
        {
            foreach (var property in MemberMap.Properties.Values)
            {
                if (!property.HasNonprivateSetter) continue;

                var body = property.SetterDecl?.Body;
                if (body == null) continue;

                AnalyzeMethod(body, property.Symbol);
            }
        }

        /// <summary>
        /// Reports a warning if any field isn't initialized with
        /// the initializer, constructor or public setters.
        /// </summary>
        void ReportUninitializedFields(ConstructorDeclarationSyntax constructorDecl)
        {
            var uninitializedSymbols =
                MemberMap.Variables
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
            // Collect initializations and warns use of uninitialized variables.
            enablesFieldDiagnostic = true;
            AnalyzeConstructor(constructorDecl, constructorSymbol);

            // Collect initializations via non-private setters to suppress constructor diagnostic.
            enablesFieldDiagnostic = false;
            AnalyzeNonprivateSetters();

            ReportUninitializedFields(constructorDecl);
        }
    }
}
