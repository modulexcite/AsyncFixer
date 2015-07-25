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
using Microsoft.CodeAnalysis.Formatting;

namespace AsyncFixer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LongRunningFixer)), Shared]
    public class LongRunningFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(Constants.LongRunningId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var memberAccess = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();

            context.RegisterCodeFix(
                CodeAction.Create("Insert Async Call", c => InsertAsyncCall(context.Document, memberAccess, c)),
                diagnostic);
        }

        private async Task<Document> InsertAsyncCall(Document document, MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
        {
            var name = memberAccess.Name.Identifier.ValueText;
            ExpressionSyntax oldNode, newNode, newMemberAccess;
            switch (name)
            {
                case "WaitAny":
                    newMemberAccess = memberAccess.WithName((SimpleNameSyntax)SyntaxFactory.ParseName("WhenAny"));
                    break;
                case "WaitAll":
                    newMemberAccess = memberAccess.WithName((SimpleNameSyntax)SyntaxFactory.ParseName("WhenAny"));
                    break;
                case "Wait":
                    newMemberAccess = memberAccess.Expression;
                    break;
                case "Result":
                    newMemberAccess = memberAccess.Expression;
                    break;
                case "Sleep":
                    newMemberAccess = SyntaxFactory.ParseExpression("Task.Delay");
                    break;
                default:
                    newMemberAccess = memberAccess.WithName((SimpleNameSyntax)SyntaxFactory.ParseName(memberAccess.Name.Identifier.ValueText + "Async"));
                    break;
            }

            var invoc = memberAccess.Parent as InvocationExpressionSyntax;
            // WaitAny, WaitAll, Wait, Sleep, XyzAsync etc.
            if (invoc != null)
            {
                oldNode = invoc;
                newNode = name == "Wait" ? newMemberAccess : invoc.WithExpression(newMemberAccess);
            }
            // t.Result
            else
            {
                oldNode = memberAccess;
                newNode = newMemberAccess;
            }

            newNode = SyntaxFactory.AwaitExpression(newNode)
                .WithAdditionalAnnotations(Formatter.Annotation)
                .WithLeadingTrivia(memberAccess.GetLeadingTrivia())
                .WithTrailingTrivia(memberAccess.GetTrailingTrivia());

            if (oldNode.Parent.Kind() == SyntaxKind.SimpleMemberAccessExpression)
            {
                newNode = SyntaxFactory.ParenthesizedExpression(newNode);
            }

            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}