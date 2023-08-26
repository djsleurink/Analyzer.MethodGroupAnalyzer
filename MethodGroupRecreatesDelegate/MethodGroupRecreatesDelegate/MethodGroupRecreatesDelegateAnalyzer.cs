using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace MethodGroupRecreatesDelegate
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MethodGroupRecreatesDelegateAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MethodGroupRecreatesDelegate";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Memory Consumption";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            //only apply the rule when target framework is a framework that still has this issue
            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var compilation = compilationStartContext.Compilation;

                
                var attributes = compilation.Assembly.GetAttributes();
                if(attributes.Length == 0)
                {
                    RegisterActions();
                    return;
                }

                var tgtFwAttr = attributes.FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == "System.Runtime.Versioning.TargetFrameworkAttribute");
                if (tgtFwAttr == null) return;

                var frameworkName = (string)tgtFwAttr.ConstructorArguments.FirstOrDefault().Value;
                if (frameworkName == null) return;

                if (SupportedFrameworks.Frameworks.Any(supportedFramework => frameworkName.StartsWith(supportedFramework)))
                {
                    RegisterActions();
                }

                void RegisterActions()
                {
                    compilationStartContext.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.ArgumentList);
                }
            });
        }


        private void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            Dictionary<ArgumentSyntax,Microsoft.CodeAnalysis.TypeInfo?> methodGroupNodes = new Dictionary<ArgumentSyntax, Microsoft.CodeAnalysis.TypeInfo?>();
            var syntaxNode = context.Node;

            if (!syntaxNode.IsKind(SyntaxKind.ArgumentList) ||
                !(syntaxNode is ArgumentListSyntax argSyntax) ||
                !(argSyntax.Arguments is SeparatedSyntaxList<ArgumentSyntax> argsList) ||
                !argsList.Any())
            {
                return;
            }

            foreach (ArgumentSyntax argumentSyntax in argsList)
            {
                var symbolTypeInfo = context.SemanticModel.GetTypeInfo(argumentSyntax.Expression);
                if (IsArgumentMethodGroup(context.SemanticModel, argumentSyntax))
                {
                    methodGroupNodes.Add(argumentSyntax, symbolTypeInfo);
                }
            }

            if (!methodGroupNodes.Any()) return; //no method group found, stop analysis

            foreach (var methodGroup in methodGroupNodes)
            {
                var invocationExpressionSyntax = methodGroup.Key.FirstAncestorOrSelf<InvocationExpressionSyntax>();

                //option 1: the node is used within a loop of any sort
                bool isInIterator = methodGroup.Key.FirstAncestorOrSelf<StatementSyntax>(x => IsNodeIteratorSyntax(x)) != null;
                if (methodGroupNodes == null || !isInIterator) return;

                //found a method group which has memory implications when used within loops, report diagnostic
                string convertedType = methodGroup.Value.GetValueOrDefault().ConvertedType?.ToDisplayString();
                var diagnostic = Diagnostic.Create(Rule, methodGroup.Key.GetLocation(), convertedType, Environment.NewLine);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsNodeIteratorSyntax(StatementSyntax node)
        {
            var nodeType = node.GetType();
            return SupportedIterators.Iterators.Any(supportedType => supportedType == nodeType);
        }

        private bool IsArgumentMethodGroup(SemanticModel semanticModel, ArgumentSyntax arg)
        {
            if (!arg.ChildNodes().Any()) return false;

            foreach (var item in arg.ChildNodes())
            {
                if (semanticModel.GetConversion(item).IsMethodGroup)
                {
                    return true;
                }
            }
            return false;
        }

    }


}
