using System.Collections.Generic;
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
using RoslynUtilities;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Formatting;

namespace AsyncFixer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnnecessaryAsyncFixer)), Shared]
    public class UnnecessaryAsyncFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(Constants.UnnecessaryAsyncId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var methodDeclaration = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            context.RegisterCodeFix(
                CodeAction.Create("Remove async/await", c => RemoveAsyncAwait(context.Document, methodDeclaration, c)),
                diagnostic);
        }

        private async Task<Document> RemoveAsyncAwait(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            MethodDeclarationSyntax newMethodDecl;

            // (1) Remove async keyword
            var asyncModifier = methodDecl.Modifiers.First(a => a.Kind() == SyntaxKind.AsyncKeyword);
            newMethodDecl = asyncModifier.HasLeadingTrivia 
                ? methodDecl.WithModifiers(methodDecl.Modifiers.Remove(asyncModifier)).WithLeadingTrivia(asyncModifier.LeadingTrivia)
                : methodDecl.WithModifiers(methodDecl.Modifiers.Remove(asyncModifier));

            // (2) If void, convert it to Task
            if (newMethodDecl.ReturnsVoid())
            {
                var newType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task").WithAdditionalAnnotations(Simplifier.Annotation).WithTrailingTrivia(newMethodDecl.ReturnType.GetTrailingTrivia());
                newMethodDecl = newMethodDecl.WithReturnType(newType);
            }

            // (3) For all await expressions, remove await and insert return if there is none. 
            var awaitExprs = newMethodDecl.Body.DescendantNodes().OfType<AwaitExpressionSyntax>();

            List<SyntaxReplacementPair> pairs = new List<SyntaxReplacementPair>();

            foreach (var awaitExpr in awaitExprs)
            {
                SyntaxNode oldNode;
                SyntaxNode newNode;
                var newAwaitExpr = awaitExpr;
                // If there is some ConfigureAwait(false), remove it 
                var invoc = awaitExpr.Expression as InvocationExpressionSyntax;
                if (invoc != null)
                {
                    var expr = invoc.Expression as MemberAccessExpressionSyntax;

                    // TODO: Check whether it is ConfigureAwait(false) or ConfigureAwait(true);
                    if (expr != null && expr.Name.Identifier.ValueText == "ConfigureAwait")
                    {
                        newAwaitExpr = awaitExpr.ReplaceNode(awaitExpr.Expression, expr.Expression);
                    }
                }

                if (awaitExpr.Parent.Kind() == SyntaxKind.ReturnStatement)
                {
                    oldNode = awaitExpr;
                    newNode = newAwaitExpr.Expression.WithAdditionalAnnotations(Simplifier.Annotation);
                }
                else
                {
                    oldNode = awaitExpr.Parent;
                    newNode = SyntaxFactory.ReturnStatement(newAwaitExpr.Expression).WithAdditionalAnnotations(Formatter.Annotation).WithTrailingTrivia(oldNode.GetTrailingTrivia());
                }
                pairs.Add(new SyntaxReplacementPair(oldNode, newNode));
            }

            newMethodDecl = newMethodDecl.ReplaceAll(pairs);

            // (4) Replace the old method with the new one.
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}