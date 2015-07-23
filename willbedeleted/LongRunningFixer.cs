using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using RoslynUtilities;

namespace AsyncFixer
{
    [ExportCodeFixProvider(LongRunningAnalyzer.DiagnosticId, LanguageNames.CSharp), Shared]
    public class LongRunningFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> GetFixableDiagnosticIds()
        {
            return ImmutableArray.Create(LongRunningAnalyzer.DiagnosticId);
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

            var memberAccess = root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();

            context.RegisterFix(
                CodeAction.Create("Insert Async Call", c => InsertAsyncCall(context.Document, memberAccess, c)),
                diagnostic);
        }

        private async Task<Document> InsertAsyncCall(Document document, MemberAccessExpressionSyntax memberAccess, CancellationToken cancellationToken)
        {
            var name = memberAccess.Name.Identifier.ValueText;
            ExpressionSyntax oldNode,newNode,newMemberAccess;
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
                    newMemberAccess = memberAccess.WithName((SimpleNameSyntax)SyntaxFactory.ParseName(memberAccess.Name.Identifier.ValueText +"Async"));
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

            if (oldNode.Parent.CSharpKind() == SyntaxKind.SimpleMemberAccessExpression)
            {
                newNode = SyntaxFactory.ParenthesizedExpression(newNode);
            }

            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}