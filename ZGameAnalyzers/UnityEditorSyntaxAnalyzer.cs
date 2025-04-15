using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ZGameAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnityEditorSyntaxAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZG0001";

    // Feel free to use raw strings if you don't need localization.
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ZG0001Title),
        Resources.ResourceManager, typeof(Resources));

    // The message that will be displayed to the user.
    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.ZG0001MessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.ZG0001Description), Resources.ResourceManager,
            typeof(Resources));

    // The category of the diagnostic (Design, Naming etc.).
    private const string Category = "UnityEditor";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);
    
    
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterSyntaxNodeAction(OnSyntaxNodeActionEditor, SyntaxKind.UsingDirective);
    }

    private void OnSyntaxNodeActionEditor(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        if (usingDirective.Name == null || usingDirective.Name.ToString() != "UnityEditor") return;
        var filePath = context.Node.SyntaxTree.FilePath;

        filePath = filePath.Replace("\\", "/");

        if (filePath.Contains("/Editor/"))
        {
            return;
        }

        if (filePath.Contains("/Library/PackageCache"))
        {
            return;
        }
        
        if (filePath.Contains("/Plugins/"))
        {
            return;
        }
      

        var root = context.Node.SyntaxTree.GetRoot(context.CancellationToken);
        var span = usingDirective.Span;

        if (IsWithinUnityEditorPreprocessorDirective(root, span)) return;
        var diagnostic = Diagnostic.Create(Rule, usingDirective.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
    
    private static bool IsWithinUnityEditorPreprocessorDirective(SyntaxNode root, TextSpan span)
    {
        var relevantDirectives = new Stack<DirectiveTriviaSyntax>();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (!trivia.IsDirective) continue;
            if (!(trivia.GetStructure() is DirectiveTriviaSyntax directive) ||
                directive.Span.Start > span.Start) continue;
            if (directive.Kind() == SyntaxKind.IfDirectiveTrivia ||
                directive.Kind() == SyntaxKind.ElifDirectiveTrivia)
            {
                relevantDirectives.Push(directive);
            }
            else if (directive.Kind() == SyntaxKind.EndIfDirectiveTrivia && relevantDirectives.Count > 0)
            {
                relevantDirectives.Pop();
            }
            else if (directive.Kind() == SyntaxKind.ElseDirectiveTrivia && relevantDirectives.Count > 0)
            {
                relevantDirectives.Pop();
                relevantDirectives.Push(directive);
            }
        }

        foreach (var directive in relevantDirectives)
        {
            if (directive.Kind() != SyntaxKind.IfDirectiveTrivia &&
                directive.Kind() != SyntaxKind.ElifDirectiveTrivia) continue;
            var condition = (directive as ConditionalDirectiveTriviaSyntax)?.Condition.ToFullString();
            if (condition != null && condition.Contains("UNITY_EDITOR"))
            {
                return true;
            }
        }

        return false;
    }
}