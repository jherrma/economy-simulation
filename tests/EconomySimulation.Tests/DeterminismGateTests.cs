using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Gates;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/08-01. V2 as a gate over real output, and — the part that matters more — the
/// evidence that the gate can fail.
///
/// A comparison that passes because it read the wrong directory, or because it compares nothing, is
/// worse than no comparison at all: it will be trusted. So these tests spend most of their length
/// on the harness rather than on the model.
/// </summary>
public sealed class DeterminismGateTests
{
    /// <summary>
    /// Two hundred households rather than a thousand. The gate's subject is the harness, and a
    /// smaller town exercises every branch of it at a fifth of the cost; the committed gate itself
    /// runs the real configuration. Through <see cref="SimulationParameters.WithHouseholds"/>,
    /// because capacity is `round(households / life)` by definition: a fifth of the town with the
    /// same shelves is five times the supply, and the run halts on a drained pool around tick 60.
    /// </summary>
    private static readonly SimulationParameters Small = SimulationParameters.Default.WithHouseholds(200);

    private static readonly int[] TwoSeeds = [1, 2];

    // ---- the gate ------------------------------------------------------------------------------

    /// <summary>All three of V2's assertions hold, on output rather than on an object graph.</summary>
    [Fact]
    public void TheGateHolds()
    {
        using var workspace = Workspace.Create("test-determinism");

        var report = DeterminismGate.Run(Small, TwoSeeds, workspace);

        Assert.True(report.Passed, report.ToString());
        Assert.Equal(3, report.Checks.Count);
    }

    /// <summary>
    /// The unused-purpose check registers a name, and a name may be registered only once. The gate
    /// therefore has to survive being run twice in one process — which a test session does.
    /// </summary>
    [Fact]
    public void TheGateCanBeRunTwiceInOneProcess()
    {
        using var first = Workspace.Create("test-determinism-twice-a");
        using var second = Workspace.Create("test-determinism-twice-b");

        var one = DeterminismGate.Run(Small, [1], first);
        var two = DeterminismGate.Run(Small, [1], second);

        Assert.True(one.Passed, one.ToString());
        Assert.True(two.Passed, two.ToString());
    }

    // ---- the comparison can fail ---------------------------------------------------------------

    /// <summary>
    /// One cent on one parameter, and the comparison says which file, which line and which column.
    ///
    /// This is the test that makes every other use of the comparison mean something. A cent on the
    /// mean income moves nothing a person would notice in a plot, and the gate has to see it.
    /// </summary>
    [Fact]
    public void AOneCentPerturbationIsReported()
    {
        using var workspace = Workspace.Create("test-one-cent");

        var perturbed = Small with
        {
            Income = Small.Income with { MeanIncome = Small.Income.MeanIncome + new Money(1) },
        };

        var baseline = Run(workspace, "baseline", Small);
        var moved = Run(workspace, "moved", perturbed);

        var difference = Comparison.FirstDifference(baseline, moved, Comparison.OutputFiles);

        Assert.NotNull(difference);
        Assert.Equal("run.csv", difference.File);
        Assert.True(difference.Row >= 2, $"the header is line 1; the difference was reported at line {difference.Row}");

        // The column is named from the header, not by position: that is what makes the report
        // something to act on rather than something to decode.
        var run = Csv.Read(Path.Combine(baseline, "run.csv"));
        Assert.True(run.Has(difference.Column), $"'{difference.Column}' is not a column of run.csv");
        Assert.NotEqual(difference.Left, difference.Right);
    }

    /// <summary>The same run twice reports nothing, so a null result means agreement rather than a comparison that never ran.</summary>
    [Fact]
    public void TwoIdenticalRunsReportNoDifference()
    {
        using var workspace = Workspace.Create("test-identical");

        var first = Run(workspace, "first", Small);
        var second = Run(workspace, "second", Small);

        Assert.Null(Comparison.FirstDifference(first, second, Comparison.OutputAndConfiguration));
    }

    /// <summary>A missing file is a difference, not a pass. This is the "read the wrong directory" failure.</summary>
    [Fact]
    public void AMissingFileIsReported()
    {
        using var workspace = Workspace.Create("test-missing");

        var first = Run(workspace, "first", Small);
        var second = Run(workspace, "second", Small);

        File.Delete(Path.Combine(second, "run.done"));

        var difference = Comparison.FirstDifference(first, second, Comparison.OutputFiles);

        Assert.NotNull(difference);
        Assert.Equal("run.done", difference.File);
        Assert.Equal("present", difference.Left);
        Assert.Equal("missing", difference.Right);
    }

    /// <summary>
    /// Byte for byte, and that includes the bytes no line carries. A platform that wrote CRLF, or
    /// dropped the final newline, would produce files every field-by-field comparison calls equal.
    /// </summary>
    [Fact]
    public void ATrailingNewlineIsADifference()
    {
        using var workspace = Workspace.Create("test-newline");

        var first = workspace.Arm("first");
        var second = workspace.Arm("second");

        File.WriteAllText(Path.Combine(first, "run.csv"), "tick,cpi\n1,1.000000\n");
        File.WriteAllText(Path.Combine(second, "run.csv"), "tick,cpi\n1,1.000000");

        var difference = Comparison.FirstDifference(first, second, ["run.csv"]);

        Assert.NotNull(difference);
        Assert.Equal("(line endings)", difference.Column);
    }

    /// <summary>A run that stopped early differs at the first line the other file does not have.</summary>
    [Fact]
    public void AShorterRunIsReportedAtTheFirstMissingRow()
    {
        using var workspace = Workspace.Create("test-shorter");

        var first = workspace.Arm("first");
        var second = workspace.Arm("second");

        File.WriteAllText(Path.Combine(first, "run.csv"), "tick,cpi\n1,1.000000\n2,1.000100\n");
        File.WriteAllText(Path.Combine(second, "run.csv"), "tick,cpi\n1,1.000000\n");

        var difference = Comparison.FirstDifference(first, second, ["run.csv"]);

        Assert.NotNull(difference);
        Assert.Equal(3, difference.Row);
        Assert.Equal("(the row itself)", difference.Column);
        Assert.Equal("(no such line)", difference.Right);
    }

    /// <summary>The marker is `key = value`, and the key names the column when there is no header.</summary>
    [Fact]
    public void AMarkerDifferenceIsNamedByItsKey()
    {
        using var workspace = Workspace.Create("test-marker");

        var first = workspace.Arm("first");
        var second = workspace.Arm("second");

        File.WriteAllText(Path.Combine(first, "run.done"), "ticks = 48\n");
        File.WriteAllText(Path.Combine(second, "run.done"), "ticks = 47\n");

        var difference = Comparison.FirstDifference(first, second, ["run.done"]);

        Assert.NotNull(difference);
        Assert.Equal("ticks", difference.Column);
    }

    private static string Run(Workspace workspace, string arm, SimulationParameters parameters)
    {
        var directory = workspace.Arm(arm);
        var run = Runs.Execute(Runs.Short(parameters), seed: 1, directory);

        Assert.True(run.IsSuccess, run.IsFailed ? run.Errors[0].Message : "");

        return directory;
    }
}
