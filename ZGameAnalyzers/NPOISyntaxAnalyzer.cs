using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZGameAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NPOISyntaxAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZG0002";

    // Feel free to use raw strings if you don't need localization.
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ZG0002Title),
        Resources.ResourceManager, typeof(Resources));

    // The message that will be displayed to the user.
    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.ZG0002MessageFormat), Resources.ResourceManager,
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
        context.RegisterSyntaxNodeAction(OnSyntaxNodeActionNPOI, SyntaxKind.UsingDirective);
    }

    private void OnSyntaxNodeActionNPOI(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        if (usingDirective.Name == null) return;

        var filePath = context.Node.SyntaxTree.FilePath;

        filePath = filePath.Replace("\\", "/");

        if (filePath.Contains("/Editor/"))
        {
            return;
        }


        if (usingDirective.Name.ToString().Contains("NPOI"))
        {
            var diagnostic = Diagnostic.Create(Rule, usingDirective.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}