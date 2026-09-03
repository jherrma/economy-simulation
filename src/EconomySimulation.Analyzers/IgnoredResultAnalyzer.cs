using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EconomySimulation.Analyzers;

/// <summary>
/// Reports a call that returns a <c>Result</c> and throws the answer away.
///
/// This is what makes "operations that can fail return a Result" more than a convention. The
/// failure it prevents is specific: a validation or a consistency check whose result is never
/// looked at is indistinguishable, at runtime, from one that passed. In a model whose whole
/// output is plausible numbers, that is not a bug anyone finds by reading the output.
///
/// Writing <c>_ = Something();</c> still compiles. Ignoring a result on purpose is fine; ignoring
/// one by forgetting is not, and the difference is exactly one visible character.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IgnoredResultAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "ES0001";

    private const string ResultBaseMetadataName = "FluentResults.ResultBase";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "A Result must be used",
        messageFormat:
            "The Result returned by '{0}' is discarded. Check it, propagate it, or write "
            + "'_ =' to say the failure is genuinely of no interest here.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "A failed Result that nobody reads is indistinguishable from a successful one, and "
            + "this model's output is plausible either way.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var resultBase = start.Compilation.GetTypeByMetadataName(ResultBaseMetadataName);
            if (resultBase is null)
            {
                // No FluentResults in this compilation; there is nothing this rule can be about.
                return;
            }

            start.RegisterOperationAction(
                operation => Analyze(operation, resultBase),
                OperationKind.ExpressionStatement);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol resultBase)
    {
        var statement = (IExpressionStatementOperation)context.Operation;

        // Unwrap `await Foo()`, so an asynchronous boundary is covered by the same rule.
        var value = statement.Operation is IAwaitOperation await ? await.Operation : statement.Operation;

        if (value is not IInvocationOperation invocation)
        {
            return;
        }

        if (!IsResult(invocation.Type, resultBase))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                invocation.TargetMethod.Name));
    }

    private static bool IsResult(ITypeSymbol? type, INamedTypeSymbol resultBase)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            var unbound = current.IsGenericType ? current.ConstructedFrom : current;

            if (SymbolEqualityComparer.Default.Equals(unbound, resultBase))
            {
                return true;
            }
        }

        return false;
    }
}
