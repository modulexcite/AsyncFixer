using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace AsyncFixer
{
    [ExportCodeFixProvider(FireForgetAnalyzer.DiagnosticId, LanguageNames.CSharp), Shared]
    public class FireForgetFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> GetFixableDiagnosticIds()
        {
            return ImmutableArray.Create(FireForgetAnalyzer.DiagnosticId);
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task ComputeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var methodDeclaration = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            context.RegisterFix(
                CodeAction.Create("Convert void to Task", c => Convert2Task(context.Document, methodDeclaration, c)),
                diagnostic);
        }

        private async Task<Document> Convert2Task(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            var newType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task").WithAdditionalAnnotations(Simplifier.Annotation).WithTrailingTrivia(methodDecl.ReturnType.GetTrailingTrivia());
            var newMethodDecl = methodDecl.WithReturnType(newType);

            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}