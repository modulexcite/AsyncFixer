using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynUtilities;

namespace AsyncFixer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnnecessaryAsyncAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "AsyncFixer001";

        internal static DiagnosticDescriptor Rule1 = new DiagnosticDescriptor(id: DiagnosticId,
            title: "Unnecessary async/await",
            messageFormat: "The method '{0}' do not need to use async/await",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule1); } }

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
                    if (exprStmt == null || exprStmt.Expression == null || exprStmt.Expression.CSharpKind() != SyntaxKind.AwaitExpression)
                        return;
                    numAwait++;
                }
                else
                {
                    foreach (var temp in returnStatements)
                    {
                        var returnStatement = (ReturnStatementSyntax)temp;
                        if (returnStatement.Expression == null || returnStatement.Expression.CSharpKind() != SyntaxKind.AwaitExpression)
                            return;
                        numAwait++;
                    }
                }
                int totalAwait = node.Body.DescendantNodes().OfType<AwaitExpressionSyntax>().Count();
                if (numAwait < totalAwait)
                    return;

                var diagnostic = Diagnostic.Create(Rule1, node.GetLocation(), node.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
