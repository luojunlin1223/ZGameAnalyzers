using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZGameAnalyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveNPOIFixProvider)), Shared]
public class RemoveNPOIFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create("ZG0002");

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;
    
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ZG0002CodeFixTitle),
        Resources.ResourceManager, typeof(Resources));

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var node = root.FindNode(diagnostic.Location.SourceSpan) as UsingDirectiveSyntax;
        if (node == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title.ToString(),
                createChangedDocument: c => RemoveUsingDirectiveAsync(context.Document, node, c),
                equivalenceKey:Title.ToString()),
            diagnostic);
    }

    private async Task<Document> RemoveUsingDirectiveAsync(Document document, UsingDirectiveSyntax usingDirective, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root.RemoveNode(usingDirective, SyntaxRemoveOptions.KeepNoTrivia);
        return document.WithSyntaxRoot(newRoot);
    }
}