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

            if (node.IsAsync() && node.Body != null && !node.HasEventArgsParameter() && !node.HasObjectStateParameter() && !node.IsTestMethod())
            {
                // If method in this form async void Xyz(object state) { ..}, ignore it!
                if (node.ParameterList != null && node.ParameterList.Parameters.Count == 1 && node.ParameterList.Parameters.First().Type.ToString() == "object")
                    return;

                if(context.SemanticModel == null)
                {
                    return;
                }

                var controlFlow = context.SemanticModel.AnalyzeControlFlow(node.Body);
                if (controlFlow == null)
                {
                    return;
                }

                var returnStatements = controlFlow.ReturnStatements;
                if (returnStatements == null)
                {
                    return;
                }

                int numAwait = 0;
                if (returnStatements.Count() == 0)
                {
                    // if awaitExpression is the last statement's expression
                    var lastStatement = node.Body.Statements.LastOrDefault();
                    if(lastStatement == null)
                    {
                        return;
                    }

                    var exprStmt = lastStatement as ExpressionStatementSyntax;
                    if (exprStmt == null || exprStmt.Expression == null || exprStmt.Expression.Kind() != SyntaxKind.AwaitExpression)
                    {
                        return;
                    }

                    numAwait++;
                }
                else
                {
                    foreach (var temp in returnStatements)
                    {
                        var returnStatement = temp as ReturnStatementSyntax;
                        if (returnStatement == null)
                        {
                            return;
                        }

                        if (returnStatement.Expression == null || returnStatement.Expression.Kind() != SyntaxKind.AwaitExpression)
                        {
                            return;
                        }

                        numAwait++;
                    }
                }

                int totalAwait = node.Body.DescendantNodes().OfType<AwaitExpressionSyntax>().Count();

                if (numAwait < totalAwait)
                {
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
