using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZGameAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GameObjectMethodAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "GO005";

    private static readonly DiagnosticDescriptor FindMethodRule = new DiagnosticDescriptor(
        DiagnosticId + "Find",
        "Avoid using GameObject.Find",
        "[ZGameAnalyzer] The method 'GameObject.Find' is used at: {0}. Use dependency injection or references instead.",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid using GameObject.Find due to its high runtime overhead.");

    private static readonly DiagnosticDescriptor SendMessageMethodRule = new DiagnosticDescriptor(
        DiagnosticId + "SendMessage",
        "Avoid using GameObject.SendMessage",
        "[ZGameAnalyzer] The method 'GameObject.SendMessage' is used at: {0}. Use direct method calls or events instead.",
        "Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid using GameObject.SendMessage due to its poor performance and lack of compile-time safety.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(FindMethodRule, SendMessageMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 注册方法调用表达式的分析
        context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
    {
        // 获取语法树文件路径
        string filePath = context.Node?.SyntaxTree?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath) || ShouldSkipFile(filePath))
        {
            return; // 跳过文件
        }

        // 确认语法节点是方法调用表达式
        if (context.Node is not InvocationExpressionSyntax invocationExpression)
            return;

        // 获取调用的方法符号
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
        if (methodSymbol == null)
            return;

        // 确认方法是否定义在 GameObject 类中
        if (methodSymbol.ContainingType?.ToDisplayString() == "UnityEngine.GameObject")
        {
            if (methodSymbol.Name == "Find")
            {
                // 检测到 GameObject.Find 生成警告
                ReportDiagnostic(context, invocationExpression, FindMethodRule);
            }
            else if (methodSymbol.Name == "SendMessage")
            {
                // 检测到 GameObject.SendMessage 生成警告
                ReportDiagnostic(context, invocationExpression, SendMessageMethodRule);
            }
        }
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, DiagnosticDescriptor rule)
    {
        var location = invocation.GetLocation();
        var message = $"{rule.Title}: {location.GetLineSpan().Path}, Line {location.GetLineSpan().StartLinePosition.Line + 1}";
        var diagnostic = Diagnostic.Create(rule, location, message);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool ShouldSkipFile(string filePath)
    {
        // 规范化路径（适用于不同平台）
        filePath = filePath.Replace('\\', '/').ToLowerInvariant();

        // 检查是否属于排除的目录
        return filePath.Contains("/packages/") ||
               filePath.Contains("/packagecache/") ||
               filePath.Contains("/editor/") ||
               filePath.Contains("/plugins/");
    }
}