// csharp
namespace ZGameAnalyzers;

using System;
using System.Collections.Generic;
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
    private const string AllowLinqSymbol = "ALLOW_LINQ";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "禁止在非 Editor 目录每帧逻辑中使用 LINQ",
        "在非 `Editor` 目录的每帧方法中不允许使用 LINQ：{0}（定义 `#define ALLOW_LINQ` 可放行）",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "项目约束：只有放在 Editor 文件夹下或显式允许的脚本可以在每帧逻辑中使用 LINQ。");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 仅注册对调用和 query 语法的检查，且最终只在“每帧方法”内报告
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

    private static bool IsExcludedPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var normalized = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToLowerInvariant();
        var segments = normalized.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(s => s == "packagecache" || s == "packages"))
            return true;

        if (segments.Any(s => s.StartsWith("com.", StringComparison.OrdinalIgnoreCase)))
            return true;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "library" && segments[i + 1] == "packagecache")
                return true;
        }

        if (segments.Any(s => s == "plugins"))
            return true;

        return false;
    }

    private static bool HasAllowLinqMacro(SyntaxTree tree)
    {
        if (tree == null)
            return false;

        var root = tree.GetRoot();
        foreach (var trivia in root.DescendantTrivia())
        {
            var kind = trivia.Kind();
            if (kind == SyntaxKind.DefineDirectiveTrivia)
            {
                if (trivia.GetStructure() is DefineDirectiveTriviaSyntax define)
                {
                    if (string.Equals(define.Name.ValueText, AllowLinqSymbol, StringComparison.Ordinal))
                        return true;
                }
            }
            else if (kind == SyntaxKind.IfDirectiveTrivia || kind == SyntaxKind.ElifDirectiveTrivia)
            {
                if (trivia.GetStructure() is IfDirectiveTriviaSyntax ifDir)
                {
                    var cond = ifDir.Condition?.ToString();
                    if (!string.IsNullOrEmpty(cond) && cond.IndexOf(AllowLinqSymbol, StringComparison.Ordinal) >= 0)
                        return true;
                }
            }
        }

        return false;
    }

    private static IMethodSymbol GetEnclosingMethodSymbol(SyntaxNode node, SemanticModel model)
    {
        var methodDecl = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (methodDecl != null)
            return model.GetDeclaredSymbol(methodDecl) as IMethodSymbol;

        var localFunc = node.AncestorsAndSelf().OfType<LocalFunctionStatementSyntax>().FirstOrDefault();
        if (localFunc != null)
            return model.GetDeclaredSymbol(localFunc) as IMethodSymbol;

        return null;
    }

    // 判断节点是否位于“每帧方法”之内，按名、attribute、继承/接口等多策略识别
    private static bool IsInPerFrameMethod(SyntaxNode node, SemanticModel model, out string methodName)
    {
        methodName = null;

        var methodDecl = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var localFunc = node.AncestorsAndSelf().OfType<LocalFunctionStatementSyntax>().FirstOrDefault();
        var anonFunc = node.AncestorsAndSelf().OfType<AnonymousFunctionExpressionSyntax>().FirstOrDefault();

        if (methodDecl != null)
            methodName = methodDecl.Identifier.ValueText;
        else if (localFunc != null)
            methodName = localFunc.Identifier.ValueText;
        else if (anonFunc != null)
            methodName = "<lambda>";

        if (string.IsNullOrEmpty(methodName))
            return false;

        var perFrameNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Update", "FixedUpdate", "LateUpdate", "OnGUI", "OnRenderObject",
            "OnPreRender", "OnPostRender", "Render", "Tick", "TickUpdate", "FrameUpdate",
            "OnUpdate", "ProcessFrame", "Execute", "Run", "HandleFrame"
        };

        if (perFrameNames.Contains(methodName))
            return true;

        var methodSymbol = GetEnclosingMethodSymbol(node, model);
        if (methodSymbol != null)
        {
            foreach (var attr in methodSymbol.GetAttributes())
            {
                var name = attr.AttributeClass?.Name ?? string.Empty;
                if (name.IndexOf("PerFrame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("EveryFrame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("UpdateLoop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ExecuteEveryFrame", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (methodSymbol.IsOverride || methodSymbol.ExplicitInterfaceImplementations.Any())
            {
                if (perFrameNames.Contains(methodSymbol.Name))
                    return true;
            }

            var containingType = methodSymbol.ContainingType;
            if (containingType != null)
            {
                var baseType = containingType;
                while (baseType != null)
                {
                    if (baseType.ToDisplayString() == "UnityEngine.MonoBehaviour")
                    {
                        if (perFrameNames.Contains(methodSymbol.Name))
                            return true;
                        break;
                    }
                    baseType = baseType.BaseType;
                }

                foreach (var iface in containingType.AllInterfaces)
                {
                    var ifaceName = iface.ToDisplayString();
                    if (ifaceName.IndexOf("IUpdate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ifaceName.IndexOf("IUpdatable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ifaceName.IndexOf("ISystem", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (perFrameNames.Contains(methodSymbol.Name) || methodSymbol.Name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;

        if (IsInEditorFolder(filePath) || IsExcludedPath(filePath))
            return;

        if (HasAllowLinqMacro(context.Node.SyntaxTree))
            return;

        var model = context.SemanticModel;
        var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null)
            return;

        var ns = symbol.ContainingNamespace;
        bool isLinq = ns != null && ns.ToDisplayString().StartsWith("System.Linq", StringComparison.Ordinal);

        var originalDef = symbol.ReducedFrom ?? symbol;
        var originalNs = originalDef?.ContainingNamespace;
        if (!isLinq && originalNs != null && originalNs.ToDisplayString().StartsWith("System.Linq", StringComparison.Ordinal))
            isLinq = true;

        if (!isLinq)
            return;

        // 只在每帧方法内报告 LINQ 使用
        if (IsInPerFrameMethod(context.Node, model, out var methodName))
        {
            var diag = Diagnostic.Create(Rule, invocation.GetLocation(), $"LINQ 方法 '{symbol.Name}' 在每帧方法 '{methodName}' 中被使用");
            context.ReportDiagnostic(diag);
        }
    }

    private static void AnalyzeQuery(SyntaxNodeAnalysisContext context)
    {
        var query = (QueryExpressionSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;

        if (IsInEditorFolder(filePath) || IsExcludedPath(filePath))
            return;

        if (HasAllowLinqMacro(context.Node.SyntaxTree))
            return;

        var model = context.SemanticModel;
        // 只在每帧方法内报告 LINQ query 语法
        if (IsInPerFrameMethod(context.Node, model, out var methodName))
        {
            var diag = Diagnostic.Create(Rule, query.GetLocation(), $"LINQ query 语法在每帧方法 '{methodName}' 中被使用");
            context.ReportDiagnostic(diag);
        }
    }
}