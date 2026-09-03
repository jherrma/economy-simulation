using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EconomySimulation.Tests.Infrastructure;

/// <summary>
/// Compiles a snippet against the engine and reports what the C# compiler said about it.
///
/// A setting in a .csproj is a claim. This is the evidence: it fails when someone relaxes a
/// setting to get an unrelated build through, which is the only way these guarantees are ever
/// actually lost.
/// </summary>
internal static class CompileFail
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    /// <summary>
    /// The compilation options the engine itself is built with, mirrored. All three matter:
    /// several of the guarantees below are warnings that only bite because
    /// TreatWarningsAsErrors promotes them — CS8509, the missing enum case, is one of them —
    /// so a harness that left warnings as warnings would report every snippet as compiling.
    /// </summary>
    private static readonly CSharpCompilationOptions Options =
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOverflowChecks(true)
            .WithGeneralDiagnosticOption(ReportDiagnostic.Error);

    /// <summary>Every diagnostic the compiler produced for <paramref name="source"/>.</summary>
    internal static ImmutableArray<Diagnostic> Diagnostics(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        return CSharpCompilation
            .Create("CompileFailProbe", [tree], References, Options)
            .GetDiagnostics();
    }

    /// <summary>
    /// Asserts that <paramref name="source"/> does not compile, and fails for exactly the
    /// reason given. A snippet that compiles is the failure mode this harness exists to catch,
    /// so it is reported as such rather than as a missing diagnostic.
    /// </summary>
    internal static void Produces(string expectedId, string source)
    {
        var errors = Diagnostics(source)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length == 0)
        {
            Assert.Fail(
                $"Expected {expectedId}, but the snippet compiled. The guarantee it was written "
                + "to prove is gone.");
        }

        Assert.True(
            errors.Any(d => d.Id == expectedId),
            $"Expected {expectedId}, got: {string.Join(", ", errors.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    /// <summary>Asserts that a snippet compiles, so a negative test cannot pass by typo.</summary>
    internal static void Compiles(string source)
    {
        var errors = Diagnostics(source)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"Expected this to compile, got: {string.Join(", ", errors.Select(d => $"{d.Id}: {d.GetMessage()}"))}");
    }

    /// <summary>Wraps statements in enough scaffolding to be a compilation unit.</summary>
    internal static string InMethod(string statements) =>
        $$"""
          using System;
          using EconomySimulation.Engine;

          internal static class Probe
          {
              internal static void Run()
              {
          {{statements}}
              }
          }
          """;

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";

        var references = platform
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        // The engine itself, so snippets can name its types. It sits beside the test
        // assembly in the output directory.
        var engine = Path.Combine(AppContext.BaseDirectory, "EconomySimulation.Engine.dll");
        if (File.Exists(engine) && references.All(r => r.Display != engine))
        {
            references.Add(MetadataReference.CreateFromFile(engine));
        }

        return [.. references];
    }
}
