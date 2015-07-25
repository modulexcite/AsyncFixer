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
    public class LongRunningAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.LongRunningTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.LongRunningMessage), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.LongRunningDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(Constants.LongRunningId, Title, MessageFormat, Constants.Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
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
