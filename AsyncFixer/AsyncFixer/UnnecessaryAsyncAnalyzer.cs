using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Diagnostics;
using RoslynUtilities;

namespace AsyncFixer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnnecessaryAsyncAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncMessage), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(Constants.UnnecessaryAsyncId, Title, MessageFormat, Constants.Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;

            if (node.IsAsync() && node.Body != null && !node.HasEventArgsParameter() && !node.HasObjectStateParameter())
            {
                // If method in this form async void Xyz(object state) { ..}, ignore it!
                if (node.ParameterList != null && node.ParameterList.Parameters.Count == 1 && node.ParameterList.Parameters.First().Type.ToString() == "object")
                    return;

                Debug.Assert(context.SemanticModel != null);
                var controlFlow = context.SemanticModel.AnalyzeControlFlow(node.Body);

                Debug.Assert(controlFlow != null);
                var returnStatements = controlFlow.ReturnStatements;

                Debug.Assert(returnStatements != null);

                int numAwait = 0;
                if (returnStatements.Count() == 0)
                {
                    // if awaitExpression is the last statement's expression
                    var lastStatement = node.Body.Statements.Last();
                    var exprStmt = lastStatement as ExpressionStatementSyntax;
                    if (exprStmt == null || exprStmt.Expression == null || exprStmt.Expression.Kind() != SyntaxKind.AwaitExpression)
                        return;
                    numAwait++;
                }
                else
                {
                    foreach (var temp in returnStatements)
                    {
                        var returnStatement = (ReturnStatementSyntax)temp;
                        if (returnStatement.Expression == null || returnStatement.Expression.Kind() != SyntaxKind.AwaitExpression)
                            return;
                        numAwait++;
                    }
                }
                int totalAwait = node.Body.DescendantNodes().OfType<AwaitExpressionSyntax>().Count();
                if (numAwait < totalAwait)
                    return;

                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
