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
    public sealed class MyTypeAnalyzer
    {
        SyntaxNodeAnalysisContext AnalysisContext { get; }

        SemanticModel SemanticModel => AnalysisContext.SemanticModel;

        public MyTypeAnalyzer(SyntaxNodeAnalysisContext analysisContext)
        {
            AnalysisContext = analysisContext;
        }

        public void Analyze(TypeDeclarationSyntax typeDecl)
        {
            var memberMap = new MemberMapCreator(SemanticModel).Create(typeDecl);

            foreach (var kv in memberMap.Constructors)
            {
                var constructorSymbol = kv.Key;
                var constructorDecl = kv.Value;

                if (memberMap.DelegatedConstructors.Contains(constructorSymbol)) continue;

                var constructorAnalyzer = new MyConstructorAnalyzer(AnalysisContext, memberMap);
                constructorAnalyzer.Analyze(constructorDecl, constructorSymbol);
            }
        }
    }
}
