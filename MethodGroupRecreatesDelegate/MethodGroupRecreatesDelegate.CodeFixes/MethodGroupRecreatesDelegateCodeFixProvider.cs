using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MethodGroupRecreatesDelegate
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MethodGroupRecreatesDelegateCodeFixProvider)), Shared]
    public class MethodGroupRecreatesDelegateCodeFixProvider : CodeFixProvider
    {
        private const string LocalVariableName = "methodGroupVariable";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MethodGroupRecreatesDelegateAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var semanticModel = await context.Document.GetSemanticModelAsync();

            // Find the type declaration identified by the diagnostic.
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var declaration = (ArgumentSyntax)root.FindNode(diagnosticSpan);
            var typeInfo = semanticModel.GetTypeInfo(declaration.Expression);

            // provide a 'Convert to local declared action/func' fix
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: string.Format(CodeFixResources.CodeFixTitle,typeInfo.ConvertedType.ToString()),
                    createChangedDocument: c => DeclareActionOutsideIteratorFix(context, semanticModel, declaration, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);

            // provide a 'Convert to static lambda' fix if language version > C#9.0
            // (but this will only ever be applicable when the target framework is .NET 5.0/.NET 6.0 because they already fixed this issue in .NET 7.0 (it's not in the SupportFrameworks))
            var compilation = (CSharpCompilation)context.Document.Project.GetCompilationAsync().Result;
            var languageVersion = compilation.LanguageVersion;
            if((int)languageVersion >= 900 && languageVersion < LanguageVersion.LatestMajor-1000)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: CodeFixResources.CodeFixTitle2,
                        createChangedDocument: c => ConvertToStaticLambdaFix(context, semanticModel, declaration, c),
                        equivalenceKey: nameof(CodeFixResources.CodeFixTitle2)),
                    diagnostic);
            }
        }

        #region DeclareActionOutsideIteratorFix
        private async Task<Document> DeclareActionOutsideIteratorFix(CodeFixContext context, SemanticModel semanticModel, ArgumentSyntax argumentToExtract, CancellationToken cancellationToken)
        {
            var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var expressionSyntax = argumentToExtract.Expression;
            var intialText = expressionSyntax.GetText();
            var iteratorNode = FindIteratorSyntax(argumentToExtract);

            //get the type of the method group
            var type = semanticModel.GetTypeInfo(argumentToExtract.Expression);
            var iteratorIndentation = iteratorNode.GetLeadingTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia));

            //replace method group identifier
            var newExpression = ReplaceExpressionWithString(expressionSyntax, LocalVariableName);
            var newRoot = root.ReplaceNode(expressionSyntax, (newExpression).Expression);

            //insert local declaration before loop begins
            var newSyntaxNodeLocalDeclaration = SyntaxFactory.ParseStatement($"var {LocalVariableName} = new {type.ConvertedType}({intialText});{Environment.NewLine}").WithLeadingTrivia(iteratorIndentation);
            var correspondingIteratorNode = newRoot.FindNode(iteratorNode.Span);
            newRoot = newRoot.InsertNodesBefore(correspondingIteratorNode, new List<SyntaxNode>() { newSyntaxNodeLocalDeclaration });
            
            var newDocument = context.Document.WithSyntaxRoot(newRoot);
            return newDocument;
        }
        private ExpressionStatementSyntax ReplaceExpressionWithString(ExpressionSyntax expression, string newExpression)
        {
            var expressionText = expression.GetText().ToString();
            var expressionIndentation = expression.GetLeadingTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia));
            var replacedString = expressionText.Replace(expressionText, newExpression);
            return (ExpressionStatementSyntax)SyntaxFactory.ParseStatement(replacedString).WithLeadingTrivia(expressionIndentation);
        }

        private StatementSyntax FindIteratorSyntax(SyntaxNode node)
        {
            return node.FirstAncestorOrSelf<StatementSyntax>(x => SupportedIterators.Iterators.Any(supportedType => supportedType == x.GetType()));
        }
        #endregion DeclareActionOutsideIteratorFix

        #region ConvertToStaticLambdaFix
        private async Task<Document> ConvertToStaticLambdaFix(CodeFixContext context, SemanticModel semanticModel, ArgumentSyntax argumentToExtract, CancellationToken cancellationToken)
        {
            var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var typeInfo = semanticModel.GetTypeInfo(argumentToExtract.Expression);
            string staticLambda = null;
            
            if (typeInfo.ConvertedType.Name == "Func")
            {
                var namedTypeSymbol = (INamedTypeSymbol)typeInfo.ConvertedType;
                var parameters = namedTypeSymbol.TypeArguments;
                staticLambda = ConstructStaticLambdaFunc(argumentToExtract, parameters);
            }
            if (typeInfo.ConvertedType.Name == "Action")
            {
                staticLambda = ConstructStaticLambdaAction(argumentToExtract);
            }


            var newNode = SyntaxFactory.ParseExpression(staticLambda);
            var newArgument = argumentToExtract.WithExpression(newNode);
            var newRoot = root.ReplaceNode(argumentToExtract, newArgument);
            var newDocument = context.Document.WithSyntaxRoot(newRoot);
            return newDocument;
        }

        private string ConstructStaticLambdaFunc(ArgumentSyntax argumentToExtract, ImmutableArray<ITypeSymbol> parameters)
        {
            ITypeSymbol returnType = parameters.Last();

            //construct parameters string and method call string
            string parametersString = "";
            string argumentsString = "";
            char currentParameterChar = 'a';
            foreach (var item in parameters.Take(parameters.Length - 1))
            {
                var parameterString = $"{item.ToDisplayString()} {currentParameterChar}";
                parametersString = string.IsNullOrEmpty(parametersString) ? parameterString : string.Join(", ", parametersString, parameterString);
                argumentsString = string.IsNullOrEmpty(argumentsString) ? $"{currentParameterChar}" : string.Join(", ", argumentsString, $"{currentParameterChar}");
                currentParameterChar++;
            }

            return $"static {returnType.ToDisplayString()} ({parametersString}) => {{ return {argumentToExtract.Expression.GetText()}({argumentsString}); }}";
            
        }

        private string ConstructStaticLambdaAction(ArgumentSyntax argumentToExtract)
        {
            return $"static () => {{ return {argumentToExtract.Expression.GetText()}(); }}";
        }
        #endregion ConvertToStaticLambdaFix
    }
}
