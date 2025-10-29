
// C#
namespace ZGameAnalyzers;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LinqInRuntimeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZG_LINQ001";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "禁止在非 Editor 目录使用 LINQ",
        "在非 `Editor` 目录中不允许使用 LINQ：{0}",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "项目约束：只有放在 Editor 文件夹下的脚本允许使用 LINQ。");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeUsing, SyntaxKind.UsingDirective);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeQuery, SyntaxKind.QueryExpression);
    }

    private static bool IsInEditorFolder(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var segments = filePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => string.Equals(s, "Editor", StringComparison.OrdinalIgnoreCase));
    }

    // 新增：排除 Unity package 路径（例如 Library/PackageCache/...、Packages/...、com.xxx@...），并排除 Assets/Plugins 等 Plugins 目录
    private static bool IsExcludedPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // 统一为目录分隔符并小写
        var normalized = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToLowerInvariant();
        var segments = normalized.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        // 包缓存或 Packages 目录
        if (segments.Any(s => s == "packagecache" || s == "packages"))
            return true;

        // 路径段以 com. 开头（典型 package 名称）
        if (segments.Any(s => s.StartsWith("com.", StringComparison.OrdinalIgnoreCase)))
            return true;

        // 包含 library/packagecache 的完整序列也视为排除
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "library" && segments[i + 1] == "packagecache")
                return true;
        }

        // 排除任意名为 Plugins 的目录（包括 `Assets/Plugins`）
        if (segments.Any(s => s == "plugins"))
            return true;

        return false;
    }

    private static void AnalyzeUsing(SyntaxNodeAnalysisContext context)
    {
        var usingNode = (UsingDirectiveSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        if (IsInEditorFolder(filePath) || IsExcludedPath(filePath))
            return;

        var name = usingNode.Name.ToString();
        if (string.Equals(name, "System.Linq", StringComparison.Ordinal) ||
            name.EndsWith(".Linq", StringComparison.Ordinal))
        {
            var diag = Diagnostic.Create(Rule, usingNode.GetLocation(), $"using {name}");
            context.ReportDiagnostic(diag);
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        if (IsInEditorFolder(filePath) || IsExcludedPath(filePath))
            return;

        var model = context.SemanticModel;
        var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null)
            return;

        var ns = symbol.ContainingNamespace;
        if (ns != null && ns.ToDisplayString().StartsWith("System.Linq", StringComparison.Ordinal))
        {
            var diag = Diagnostic.Create(Rule, invocation.GetLocation(), $"LINQ method '{symbol.Name}'");
            context.ReportDiagnostic(diag);
            return;
        }

        var originalDef = symbol.ReducedFrom ?? symbol;
        var originalNs = originalDef.ContainingNamespace;
        if (originalNs != null && originalNs.ToDisplayString().StartsWith("System.Linq", StringComparison.Ordinal))
        {
            var diag = Diagnostic.Create(Rule, invocation.GetLocation(), $"LINQ method '{symbol.Name}'");
            context.ReportDiagnostic(diag);
        }
    }

    private static void AnalyzeQuery(SyntaxNodeAnalysisContext context)
    {
        var query = (QueryExpressionSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        if (IsInEditorFolder(filePath) || IsExcludedPath(filePath))
            return;

        var diag = Diagnostic.Create(Rule, query.GetLocation(), "LINQ query syntax");
        context.ReportDiagnostic(diag);
    }
}
