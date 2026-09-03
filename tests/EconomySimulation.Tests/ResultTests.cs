using EconomySimulation.Analyzers;
using EconomySimulation.Engine;
using EconomySimulation.Tests.Infrastructure;
using FluentResults;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/01-04.</summary>
public sealed class ResultTests
{
    private const string Preamble =
        """
        using FluentResults;

        internal static class Boundary
        {
            internal static Result Check() => Result.Ok();
            internal static Result<int> Load() => Result.Ok(1);
            internal static int Ordinary() => 1;
        }
        """;

    private static string WithBody(string statements) =>
        $$"""
          {{Preamble}}

          internal static class Caller
          {
              internal static void Run()
              {
          {{statements}}
              }
          }
          """;

    // ---- an ignored Result does not build ------------------------------------------------

    [Fact]
    public void AnIgnoredResult_IsABuildError()
    {
        AnalyzerProbe.Reports(
            "ES0001",
            new IgnoredResultAnalyzer(),
            WithBody("        Boundary.Check();"));
    }

    [Fact]
    public void AnIgnoredGenericResult_IsABuildError()
    {
        AnalyzerProbe.Reports(
            "ES0001",
            new IgnoredResultAnalyzer(),
            WithBody("        Boundary.Load();"));
    }

    /// <summary>
    /// The escape hatch. Ignoring a failure on purpose is allowed; ignoring one by forgetting is
    /// not, and the difference between the two is one visible character in the diff.
    /// </summary>
    [Fact]
    public void AnExplicitlyDiscardedResult_IsAllowed()
    {
        AnalyzerProbe.ReportsNothing(
            new IgnoredResultAnalyzer(),
            WithBody("        _ = Boundary.Check();"));
    }

    [Fact]
    public void AUsedResult_IsAllowed()
    {
        AnalyzerProbe.ReportsNothing(
            new IgnoredResultAnalyzer(),
            WithBody("        var r = Boundary.Check();\n        if (r.IsFailed) { return; }"));
    }

    /// <summary>
    /// The rule has to be about Result specifically. An analyser that fires on every discarded
    /// return value would be turned off within a week, and then it protects nothing.
    /// </summary>
    [Fact]
    public void AnIgnoredOrdinaryReturnValue_IsNotTheAnalysersBusiness()
    {
        AnalyzerProbe.ReportsNothing(
            new IgnoredResultAnalyzer(),
            WithBody("        Boundary.Ordinary();"));
    }

    /// <summary>The engine is built with the analyser attached, so this is not hypothetical.</summary>
    [Fact]
    public void TheEngineIsBuiltWithTheAnalyserAttached()
    {
        var csproj = File.ReadAllText(
            Path.Combine(Repo.EngineSourceDirectory, "EconomySimulation.Engine.csproj"));

        Assert.Contains("OutputItemType=\"Analyzer\"", csproj, StringComparison.Ordinal);
        Assert.Contains("EconomySimulation.Analyzers", csproj, StringComparison.Ordinal);
    }

    // ---- failures accumulate, and say something ------------------------------------------

    /// <summary>
    /// Three problems in a configuration report as three problems. Returning on the first is the
    /// difference between one edit and a dozen run-fix-rerun cycles.
    /// </summary>
    [Fact]
    public void ThreeProblems_ReportAsThree()
    {
        var result = new Validation()
            .Require(false, "households", "at least 1", 0)
            .Require(true, "ticks", "at least 1", 360)
            .Require(false, "mean_income", "greater than zero", -5)
            .Require(false, "seeds", "at least 1", 0)
            .ToResult();

        Assert.True(result.IsFailed);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void EveryFailureNamesTheSubject_TheExpectation_AndWhatWasThere()
    {
        var result = new Validation()
            .Require(false, "abstainer_share", "between 0 and 1", 1.5)
            .ToResult();

        Assert.Equal(
            "abstainer_share: expected between 0 and 1, got 1.5",
            result.Errors.Single().Message);
    }

    /// <summary>
    /// The message has to survive a machine whose culture writes 1,5 for one and a half — the
    /// same reason Money formats invariantly.
    /// </summary>
    [Fact]
    public void FailureMessages_AreInvariantCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");

            var result = new Validation().Fail("k", "at most 1", 0.05).ToResult();

            Assert.Equal("k: expected at most 1, got 0.05", result.Errors.Single().Message);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void NothingWrong_IsSuccess_AndTheValueIsOnlyBuiltThen()
    {
        var built = 0;

        var result = new Validation()
            .Require(true, "ticks", "at least 1", 360)
            .ToResult(() => ++built);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(1, built);
    }

    [Fact]
    public void SomethingWrong_DoesNotBuildTheValue()
    {
        var built = 0;

        var result = new Validation()
            .Require(false, "ticks", "at least 1", 0)
            .ToResult(() => ++built);

        Assert.True(result.IsFailed);
        Assert.Equal(0, built);
    }

    [Fact]
    public void Absorb_KeepsTheMessagesOfTheOperationItWraps()
    {
        var inner = new Validation().Fail("goods.food.life", "at least 1", 0).ToResult();

        var outer = new Validation()
            .Require(false, "seeds", "at least 1", 0)
            .Absorb(inner)
            .ToResult();

        Assert.Equal(2, outer.Errors.Count);
        Assert.Contains(outer.Errors, e => e.Message.Contains("goods.food.life", StringComparison.Ordinal));
    }

    [Fact]
    public void AFailureWithNoSubject_IsRejectedAtTheSource()
    {
        Assert.Throws<ArgumentException>(() => new Validation().Fail("  ", "something", 1));
    }
}
