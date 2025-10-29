namespace ZGameAnalyzers;

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AnnotateLinqInvocationFixProvider)), Shared]
public class AnnotateLinqInvocationFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(LinqInRuntimeAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diag = context.Diagnostics[0];
        var node = root.FindNode(diag.Location.SourceSpan);

        // 只对 InvocationExpression 或 QueryExpression 提供注释修复（安全、非破坏性）
        if (node is InvocationExpressionSyntax || node is QueryExpressionSyntax)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "在此行前添加 LINQ 使用提示注释",
                    createChangedDocument: c => AddCommentAsync(context.Document, node, c),
                    equivalenceKey: "AnnotateLinqInvocation"),
                context.Diagnostics);
        }
    }

    private async Task<Document> AddCommentAsync(Document document, SyntaxNode node,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var leading = node.GetLeadingTrivia();
        var commentTrivia = SyntaxFactory.Comment("// TODO: LINQ 在 Runtime 目录中不允许使用，请替换为等价循环或手动实现");
        var newLeading = leading.Insert(0, commentTrivia).Insert(1, SyntaxFactory.ElasticCarriageReturnLineFeed);
        var newNode = node.WithLeadingTrivia(newLeading);
        var newRoot = root.ReplaceNode(node, newNode);
        return document.WithSyntaxRoot(newRoot);
    }
}