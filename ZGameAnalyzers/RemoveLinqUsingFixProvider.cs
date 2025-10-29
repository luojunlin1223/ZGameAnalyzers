namespace ZGameAnalyzers;

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveLinqUsingFixProvider)), Shared]
public class RemoveLinqUsingFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(LinqInRuntimeAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diag = context.Diagnostics[0];
        var node = root.FindNode(diag.Location.SourceSpan);

        if (node is UsingDirectiveSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "移除 `using System.Linq`",
                    createChangedDocument: c => RemoveUsingAsync(context.Document, (UsingDirectiveSyntax)node, c),
                    equivalenceKey: "RemoveLinqUsing"),
                context.Diagnostics);
        }
    }

    private async Task<Document> RemoveUsingAsync(Document document, UsingDirectiveSyntax usingNode, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root.RemoveNode(usingNode, Microsoft.CodeAnalysis.SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(newRoot);
    }
}