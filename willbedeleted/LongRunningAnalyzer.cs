using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynUtilities;

namespace AsyncFixer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LongRunningAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "AsyncFixer002";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(id: DiagnosticId,
            title: "Long running / blocking operations under an async method",
            messageFormat: "{0} should be used instead of {1}.",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.SimpleMemberAccessExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            bool report = false;
            string replacement = "";
            var node = (MemberAccessExpressionSyntax)context.Node;

            //var method = context.SemanticModel.GetEnclosingSymbol(node.SpanStart) as IMethodSymbol;
            var methodSyntax = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            if (methodSyntax != null && methodSyntax.IsAsync())
            {
                var invokeMethod = context.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;

                if (invokeMethod != null && invokeMethod.Name != "Invoke")
                {
                    replacement = DetectSynchronousUsages(((IMethodSymbol)invokeMethod.OriginalDefinition), context.SemanticModel);
                    report = replacement != "None";
                }

                var property = context.SemanticModel.GetSymbolInfo(node).Symbol as IPropertySymbol;

                if (property != null && property.OriginalDefinition.ContainingType.Name == "Task" && property.OriginalDefinition.Name == "Result")
                {
                    var name = node.Expression.ToString();
                    if (!methodSyntax.DescendantNodes().OfType<AwaitExpressionSyntax>().Any(awaitExpr =>
                            awaitExpr.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier =>
                                identifier.ToString() == name)))
                    {
                        replacement = "await";
                        report = true;
                    }
                }

                if (report)
                {
                    var diagnostic = Diagnostic.Create(Rule, node.Name.GetLocation(), replacement, node.Name.ToString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static string DetectSynchronousUsages(IMethodSymbol methodCallSymbol, SemanticModel semanticModel)
        {
            var list = semanticModel.LookupSymbols(0, container: methodCallSymbol.ContainingType,
                                includeReducedExtensionMethods: true);

            var name = methodCallSymbol.Name;

            if (methodCallSymbol.ContainingType.Name == "Thread" && name == "Sleep")
            {
                return "Task.Delay";
            }
            // Parameter Sifir olmasi lazim!!!!!
            else if (methodCallSymbol.ContainingType.Name == "Task" && name == "Wait")
            {
                return "await";
            }
            else if (methodCallSymbol.ContainingType.Name == "Task" && name == "WaitAll")
            {
                return "Task.WhenAll";
            }
            else if (methodCallSymbol.ContainingType.Name == "Task" && name == "WaitAny")
            {
                return "Task.WhenAny";
            }


            foreach (var tmp in list)
            {
                if (tmp.Name.Equals(name + "Async"))
                {
                    return tmp.Name;
                }
            }
            return "None";
        }
    }
}
