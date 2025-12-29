using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZGameAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EmptyUnityMethodAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "GO004";

    private static readonly DiagnosticDescriptor EmptyMethodRule = new DiagnosticDescriptor(
        DiagnosticId,
        "Detected empty Unity method",
        "[ZGameAnalyzer] The Unity method '{0}' is empty and should be removed or implemented.",
        "CodeQuality",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Empty Unity lifecycle methods such as Awake, Start, and Update should either be implemented or removed to reduce noise and improve code clarity.");

    // 所有需要检测的 Unity 生命周期方法
    private static readonly HashSet<string> UnityLifecycleMethods = new()
    {
        "Awake", "Start", "OnEnable", "Update", "FixedUpdate", "LateUpdate", "OnGUI", "OnDisable", "OnDestroy"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(EmptyMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 注册方法声明的分析器
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDecl)
            return;

        // 获取方法名称
        var methodName = methodDecl.Identifier.Text;

        // 如果不是我们关心的生命周期函数，跳过
        if (!UnityLifecycleMethods.Contains(methodName))
            return;

        // 获取方法的主体（Body）
        var body = methodDecl.Body;

        // 检查是否为 MonoBehaviour 子类
        var containingClass = methodDecl.Parent as ClassDeclarationSyntax;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);

        if (!IsMonoBehaviour(classSymbol))
            return; // 如果不是继承自 MonoBehaviour 的类，跳过

        // 检查方法是否为空
        if (IsEmptyMethod(body))
        {
            // 生成诊断报告
            var diagnostic = Diagnostic.Create(
                EmptyMethodRule,
                methodDecl.Identifier.GetLocation(),
                methodName);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsEmptyMethod(BlockSyntax body)
    {
        // 如果方法没有定义 Body 或者 Body 内部没有语句，则认为是空方法
        if (body == null || !body.Statements.Any())
            return true;

        // 检查 Body 内是否只有注释或空语句
        foreach (var statement in body.Statements)
        {
            // 检查语句是否有效：忽略空语句和被注释的语句
            if (statement is not EmptyStatementSyntax && !IsPurelyComment(statement))
            {
                return false; // 有效语句，方法不是空的
            }
        }

        return true; // 如果方法体内只包含注释或空白内容
    }

    private static bool IsPurelyComment(StatementSyntax statement)
    {
        // 获取所有 Trivia，包括前导和后导
        var allTrivia = statement.GetLeadingTrivia().AddRange(statement.GetTrailingTrivia());

        foreach (var trivia in allTrivia)
        {
            // 如果 `Trivia` 的种类不是注释或空白，则此语句不是“纯注释”
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return false; // 包含非注释内容的有效语句
            }
        }

        // 如果所有 Trivia 均为注释或空白，则此语句是仅包含注释的
        return true;
    }

    private static bool IsMonoBehaviour(INamedTypeSymbol classSymbol)
    {
        while (classSymbol != null)
        {
            if (classSymbol.ToDisplayString() == "UnityEngine.MonoBehaviour")
                return true;

            classSymbol = classSymbol.BaseType;
        }

        return false;
    }
}