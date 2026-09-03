using EconomySimulation.Engine.Configuration;
using EconomySimulation.Gates;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/08-03. V4: the creditless baseline sits still after the warm-up.
///
/// The committed gate runs the real configuration over all thirty seeds. These tests are the two
/// things a passing run cannot show: that the gate catches a price rule which is not converging,
/// and that the statistics underneath it measure what they claim to.
/// </summary>
public sealed class NullRunGateTests
{
    /// <summary>A town of two hundred over a hundred and eighty ticks, shelves stocked for two hundred.</summary>
    private static readonly SimulationParameters Small = SimulationParameters.Default.WithHouseholds(200) with
    {
        Run = SimulationParameters.Default.Run with { Households = 200, Ticks = 180, WarmupTicks = 60 },
    };

    private static readonly int[] SixSeeds = [1, 2, 3, 4, 5, 6];

    // ---- the gate can fail ---------------------------------------------------------------------

    /// <summary>
    /// `k` set high enough to oscillate, and the gate says so.
    ///
    /// This is the failure a trend test cannot catch on its own: an oscillation goes nowhere, so it
    /// has no trend at all. What gives it away is the band — at the default `k` the CPI moves within
    /// a couple of per cent over the window, and at `k = 0.9` it swings by more than a hundred.
    /// </summary>
    [Fact]
    public void APriceRuleThatOscillatesIsCaught()
    {
        using var workspace = Workspace.Create("test-oscillating-k");

        var hunting = Small with { Prices = Small.Prices with { K = 0.9 } };
        var report = NullRunGate.Run(hunting, SixSeeds, workspace);

        Assert.False(report.Passed, report.ToString());
        Assert.False(Cpi(report).Passed, "the CPI check should be what catches it: " + report);
        Assert.Contains(Cpi(report).Failures, f => f.Contains("peak to trough", StringComparison.Ordinal));
    }

    /// <summary>
    /// And at the specification's `k` the same town's CPI is flat — so the failure above is the
    /// adjustment speed and not the town.
    /// </summary>
    [Fact]
    public void TheSameTownAtTheSpecifiedKIsFlat()
    {
        using var workspace = Workspace.Create("test-settled-k");

        var report = NullRunGate.Run(Small, SixSeeds, workspace);

        Assert.True(Cpi(report).Passed, Cpi(report).Name + ": " + report);
    }

    /// <summary>The null run is the creditless baseline by definition. Handed anything else, it refuses rather than measuring it.</summary>
    [Fact]
    public void ARunWithCreditIsRefused()
    {
        using var workspace = Workspace.Create("test-credit-on");

        var lending = Small with { Credit = Small.Credit with { CreditEnabled = true } };
        var report = NullRunGate.Run(lending, SixSeeds, workspace);

        Assert.False(report.Passed);
        Assert.Contains(report.Checks[0].Failures, f => f.Contains("credit_enabled is true", StringComparison.Ordinal));
    }

    // ---- the statistics ------------------------------------------------------------------------

    /// <summary>Drift is the movement of the least-squares line across the window, relative to the mean — not a slope per tick, which is a number nobody can read against a tolerance.</summary>
    [Fact]
    public void DriftIsTheTotalMovementAcrossTheWindow()
    {
        // 100, 101, … 199: the line rises by 99 about a mean of 149.5.
        var rising = new double[100];

        for (var i = 0; i < rising.Length; i++)
        {
            rising[i] = 100 + i;
        }

        Assert.Equal(99.0 / 149.5, Stationarity.Drift(rising), 9);
        Assert.Equal(0.0, Stationarity.Drift([5.0, 5.0, 5.0, 5.0]), 9);

        // An oscillation goes nowhere: no trend at all, which is exactly why the band is needed.
        // Seven points rather than six, so the series ends where it began — an even number of
        // half-cycles is a ramp with a wobble on it, and the least-squares line rightly sees one.
        Assert.Equal(0.0, Stationarity.Drift([1.0, 2.0, 1.0, 2.0, 1.0, 2.0, 1.0]), 9);
    }

    /// <summary>The band is peak to trough over the mean, and it is what an oscillation shows up in.</summary>
    [Fact]
    public void TheBandIsPeakToTrough()
    {
        Assert.Equal(0.0, Stationarity.Band([3.0, 3.0, 3.0]), 9);

        // 1 and 2 about a mean of 1.5: two thirds.
        Assert.Equal(2.0 / 3.0, Stationarity.Band([1.0, 2.0, 1.0, 2.0]), 9);
    }

    private static GateCheck Cpi(GateReport report) =>
        report.Checks.First(c => c.Name.StartsWith("the CPI", StringComparison.Ordinal));
}
