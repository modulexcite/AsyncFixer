using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynUtilities;

namespace AsyncFixer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FireForgetAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "AsyncFixer003";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(id: DiagnosticId,
            title: "Avoid Fire&Forget Async Methods",
            messageFormat: "{0} is a fire&forget async method. It should return non-void.",
            category: "AsyncUsage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;

            if (node.IsAsync() && node.ReturnsVoid() && !node.HasEventArgsParameter() && !node.HasObjectStateParameter())
            {
                var diagnostic = Diagnostic.Create(Rule, node.ReturnType.GetLocation(), node.Identifier.ValueText);
                context.ReportDiagnostic(diagnostic);
            }
        }

    }
}
