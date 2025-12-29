using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZGameAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StartCoroutineUsageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "GO003";

        private static readonly DiagnosticDescriptor StartCoroutineRule = new DiagnosticDescriptor(
            DiagnosticId,
            "Detected StartCoroutine Usage",
            "[ZGameAnalyzer] A StartCoroutine call is found at: {0}. Consider using alternative patterns such as async/await for better readability and control.",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Avoid directly calling StartCoroutine to improve performance and readability. Prefer using async/await-based methods.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(StartCoroutineRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // 注册方法调用表达式的处理
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            // 获取语法节点
            if (context.Node is not InvocationExpressionSyntax invocationExpression)
                return;

            // 获取调用的方法符号
            var symbol = context.SemanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;

            // 检查方法符号是否为 StartCoroutine
            if (symbol != null && symbol.Name == "StartCoroutine")
            {
                // 确认方法是否定义在 UnityEngine.MonoBehaviour 类中
                if (IsStartCoroutineFromMonoBehaviour(symbol))
                {
                    // 获取文件路径
                    var filePath = invocationExpression.SyntaxTree.FilePath;

                    // 跳过特定目录文件（比如 Editor, Packages, Plugins）
                    if (!string.IsNullOrWhiteSpace(filePath) && ShouldSkipFile(filePath))
                        return;

                    // 生成诊断信息
                    var location = invocationExpression.GetLocation();
                    var message = $"Detected StartCoroutine usage at: {filePath}";
                    var diagnostic = Diagnostic.Create(StartCoroutineRule, location, message);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsStartCoroutineFromMonoBehaviour(IMethodSymbol methodSymbol)
        {
            // 确保目标方法 (StartCoroutine) 来自 UnityEngine.MonoBehaviour
            var containingType = methodSymbol.ContainingType;
            while (containingType != null)
            {
                if (containingType.ToDisplayString() == "UnityEngine.MonoBehaviour")
                    return true;

                containingType = containingType.BaseType;
            }

            return false;
        }

        private static bool ShouldSkipFile(string filePath)
        {
            // 标准化路径以适应不同操作系统
            filePath = filePath.Replace('\\', '/').ToLowerInvariant();

            // 检查是否属于排除的目录
            return filePath.Contains("/packages/") ||
                   filePath.Contains("/packagecache/") ||
                   filePath.Contains("/editor/") ||
                   filePath.Contains("/plugins/");
        }
    }
}