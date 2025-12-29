using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZGameAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GameObjectAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "GO001";

    private static readonly DiagnosticDescriptor TagRule = new DiagnosticDescriptor(
        DiagnosticId,
        "Detected GameObject.tag Usage",
        "[ZGameAnalyzer] <color=red>The 'tag' property of GameObject is used at: {0}</color>",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Check if 'GameObject.tag' is being used, as it may have performance implications.");

    private static readonly DiagnosticDescriptor NameRule = new DiagnosticDescriptor(
        DiagnosticId + "NAME",
        "Detected GameObject.name Usage",
        "The 'name' property of GameObject is used at: {0}",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Check if 'GameObject.name' is being used.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(TagRule, NameRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 注册属性访问表达式的检测
        context.RegisterSyntaxNodeAction(AnalyzePropertyAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzePropertyAccess(SyntaxNodeAnalysisContext context)
    {
        // 获取文件路径
        var filePath = context.Node.SyntaxTree.FilePath;
        if (string.IsNullOrWhiteSpace(filePath) || ShouldSkipFile(filePath))
        {
            return; // 如果文件路径为空或者路径应该被忽略，直接跳过分析
        }

        // 确认访问表达式是否是成员访问表达式 (e.g., "gameObject.tag" 或 "gameObject.name")
        if (context.Node is MemberAccessExpressionSyntax memberAccess)
        {
            var symbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, memberAccess).Symbol;
            if (symbol is IPropertySymbol propertySymbol)
            {
                // 确保成员属于 UnityEngine.GameObject 类型
                if (IsGameObjectMember(propertySymbol, context.SemanticModel))
                {
                    // 判断是否为 tag 或 name 属性
                    if (propertySymbol.Name == "tag")
                    {
                        // 报告 tag 属性使用
                        var diagnostic = Diagnostic.Create(
                            TagRule,
                            memberAccess.GetLocation(),
                            memberAccess.ToString());
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (propertySymbol.Name == "name")
                    {
                        // 报告 name 属性使用
                        var diagnostic = Diagnostic.Create(
                            NameRule,
                            memberAccess.GetLocation(),
                            memberAccess.ToString());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    private static bool IsGameObjectMember(IPropertySymbol propertySymbol, SemanticModel semanticModel)
    {
        // 确认属性的封闭类型是否为 UnityEngine.GameObject
        var containingType = propertySymbol.ContainingType;
        return containingType?.ToDisplayString() == "UnityEngine.GameObject";
    }

    private static bool ShouldSkipFile(string filePath)
    {
        // 标准化路径分隔符（支持跨平台的路径）
        filePath = filePath.Replace('\\', '/').ToLowerInvariant();

        // 检查是否为 Editor 或 Plugins 文件夹
        if (filePath.Contains("/editor/") || filePath.Contains("/plugins/"))
        {
            return true; // 跳过这些文件
        }

        return false;
    }
}