using EconomySimulation.Engine.Configuration;
using EconomySimulation.Gates;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/08-04. V5: `credit_high` with every θ forced to zero reproduces `credit_off`
/// byte for byte.
///
/// Most of these tests are about the baselines rather than about the model, because a baseline is
/// the one kind of test that can rot silently. One taken under a schema that no longer exists, or
/// one that quietly rewrites itself when it disagrees, records nothing at all — and the first change
/// it would have caught is the one that broke it.
/// </summary>
public sealed class CreditOffGateTests
{
    /// <summary>Two hundred households, shelves stocked for two hundred. The gates run the real configuration.</summary>
    private static readonly SimulationParameters Small = SimulationParameters.Default.WithHouseholds(200);

    private static readonly int[] TwoSeeds = [1, 2];

    // ---- the claim itself ----------------------------------------------------------------------

    /// <summary>
    /// Credit switched on and never reached is credit switched off, to the byte.
    ///
    /// With V2's undrawn-purpose check beside it, this is also what says the θ draws cost nothing:
    /// the treatment arm draws θ for every household and a coin for every financeable candidate it
    /// considers, and the output does not move by a single character.
    /// </summary>
    [Fact]
    public void ThetaForcedToZeroReproducesTheBaseline()
    {
        using var workspace = Workspace.Create("test-theta-zero");

        var report = CreditOffGate.Run(Small, TwoSeeds, workspace, Fresh(workspace));

        Assert.True(report.Passed, report.ToString());
    }

    /// <summary>The gate would be worthless if its two arms were the same configuration, so it says out loud that they are not.</summary>
    [Fact]
    public void TheTwoArmsAreDifferentConfigurations()
    {
        using var workspace = Workspace.Create("test-arms-differ");

        var report = CreditOffGate.Run(Small, [1], workspace, Fresh(workspace));
        var arms = report.Checks[0];

        Assert.True(arms.Passed, arms.Name);
        Assert.Contains(arms.Observations, o => o.Contains("credit_enabled = true", StringComparison.Ordinal));
        Assert.Contains(arms.Observations, o => o.Contains("theta = 0", StringComparison.Ordinal));
    }

    // ---- the baselines -------------------------------------------------------------------------

    /// <summary>A baseline that has moved is reported, with the file, the line and the column that moved.</summary>
    [Fact]
    public void ABaselineTheModelHasMovedAwayFromIsReported()
    {
        using var workspace = Workspace.Create("test-moved");

        var baselines = Fresh(workspace);
        var run = Path.Combine(Runs.Directory(baselines, 1), "run.csv");
        var lines = File.ReadAllLines(run);

        // One cent on one cell of one row of one seed.
        lines[2] = lines[2].Replace(",0.00,", ",0.01,", StringComparison.Ordinal);
        File.WriteAllLines(run, lines);

        var report = CreditOffGate.Run(Small, TwoSeeds, workspace, baselines);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Baselines(report).Failures, f => f.Contains("run.csv", StringComparison.Ordinal));
    }

    /// <summary>
    /// A baseline whose configuration no longer describes this schema fails loudly rather than
    /// being compared anyway.
    ///
    /// A parameter removed here stands for any schema change. Comparing across one would pass or
    /// fail for reasons that have nothing to do with the model, which is the worst thing a baseline
    /// can do: it is trusted.
    /// </summary>
    [Fact]
    public void ABaselineWhoseConfigurationNoLongerFitsTheSchemaIsRefused()
    {
        using var workspace = Workspace.Create("test-stale-schema");

        var baselines = Fresh(workspace);
        var configuration = Path.Combine(Runs.Directory(baselines, 1), "effective-config.toml");
        var text = File.ReadAllText(configuration);

        File.WriteAllText(
            configuration,
            text.Replace("buffer_months = 2.0\n", string.Empty, StringComparison.Ordinal));

        var report = CreditOffGate.Run(Small, TwoSeeds, workspace, baselines);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Baselines(report).Failures, f => f.Contains("round-trips", StringComparison.Ordinal));
    }

    /// <summary>A baseline the loader cannot read at all is refused before anything is compared.</summary>
    [Fact]
    public void ABaselineWhoseConfigurationWillNotLoadIsRefused()
    {
        using var workspace = Workspace.Create("test-unloadable");

        var baselines = Fresh(workspace);
        File.WriteAllText(Path.Combine(Runs.Directory(baselines, 1), "effective-config.toml"), "this is not toml =\n");

        var report = CreditOffGate.Run(Small, TwoSeeds, workspace, baselines);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Baselines(report).Failures, f => f.Contains("no longer loads", StringComparison.Ordinal));
    }

    /// <summary>A baseline taken under different parameters is refused: comparing it would report the configuration, not the model.</summary>
    [Fact]
    public void ABaselineTakenUnderDifferentParametersIsRefused()
    {
        using var workspace = Workspace.Create("test-other-parameters");

        var baselines = Fresh(workspace);
        var elsewhere = Small with { Decision = Small.Decision with { Lambda = 1.05 } };
        var report = CreditOffGate.Run(elsewhere, TwoSeeds, workspace, baselines);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Baselines(report).Failures, f => f.Contains("different parameters", StringComparison.Ordinal));
    }

    /// <summary>A seed with no baseline is reported rather than quietly skipped.</summary>
    [Fact]
    public void AMissingSeedIsReportedRatherThanSkipped()
    {
        using var workspace = Workspace.Create("test-missing-seed");

        var baselines = Fresh(workspace);
        Directory.Delete(Runs.Directory(baselines, 2), recursive: true);

        var report = CreditOffGate.Run(Small, TwoSeeds, workspace, baselines);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Baselines(report).Failures, f => f.Contains("no baseline for seed 2", StringComparison.Ordinal));
    }

    /// <summary>
    /// **A failing comparison does not repair the baseline.** This is the whole reason regenerating
    /// is a separate command: a baseline that rewrites itself when it disagrees records nothing, and
    /// the first change it would have caught is the one that deletes it.
    /// </summary>
    [Fact]
    public void AFailingComparisonLeavesTheBaselineAlone()
    {
        using var workspace = Workspace.Create("test-no-self-repair");

        var baselines = Fresh(workspace);
        var run = Path.Combine(Runs.Directory(baselines, 1), "run.csv");
        var tampered = File.ReadAllText(run).Replace(",0.00,", ",0.01,", StringComparison.Ordinal);

        File.WriteAllText(run, tampered);

        var report = CreditOffGate.Run(Small, TwoSeeds, workspace, baselines);

        Assert.False(report.Passed);
        Assert.Equal(tampered, File.ReadAllText(run));
    }

    /// <summary>Regenerating writes what the run produced, and records the commit and the day beside it.</summary>
    [Fact]
    public void RebaselineWritesTheRunAndItsProvenance()
    {
        using var workspace = Workspace.Create("test-rebaseline");

        var baselines = Path.Combine(workspace.Root, "written");
        var report = CreditOffGate.Rebaseline(Small, TwoSeeds, baselines);

        Assert.True(report.Passed, report.ToString());

        var provenance = File.ReadAllText(Path.Combine(baselines, CreditOffGate.ProvenanceFile));

        Assert.Contains("commit = ", provenance, StringComparison.Ordinal);
        Assert.Contains("taken = ", provenance, StringComparison.Ordinal);
        Assert.Contains("ticks = 48", provenance, StringComparison.Ordinal);

        foreach (var seed in TwoSeeds)
        {
            Assert.True(File.Exists(Path.Combine(Runs.Directory(baselines, seed), "run.done")));
        }
    }

    /// <summary>Baselines nobody has taken are a failure, not a pass. An absent comparison must never read as agreement.</summary>
    [Fact]
    public void MissingBaselinesAreAFailure()
    {
        using var workspace = Workspace.Create("test-no-baselines");

        var report = CreditOffGate.Run(Small, [1], workspace, Path.Combine(workspace.Root, "nothing-here"));

        Assert.False(report.Passed);
        Assert.Contains(Baselines(report).Failures, f => f.Contains("rebaseline", StringComparison.Ordinal));
    }

    // ---- the committed baselines this repository carries ----------------------------------------

    /// <summary>
    /// The baselines under version control are the ones the default configuration produces. If this
    /// fails, either the model moved or the parameters did, and the commit that did it should say
    /// which.
    /// </summary>
    [Fact]
    public void TheCommittedBaselinesStillMatch()
    {
        using var workspace = Workspace.Create("test-committed");

        var report = CreditOffGate.Run(SimulationParameters.Default, Runs.ShortSeeds, workspace);

        Assert.True(report.Passed, report.ToString());
    }

    private static GateCheck Baselines(GateReport report) =>
        report.Checks.First(c => c.Name.Contains("committed baselines", StringComparison.Ordinal));

    /// <summary>A baseline set of this test's own, so that staging a stale one does not touch the repository's.</summary>
    private static string Fresh(Workspace workspace)
    {
        var baselines = Path.Combine(workspace.Root, "baselines");
        var written = CreditOffGate.Rebaseline(Small, TwoSeeds, baselines);

        Assert.True(written.Passed, written.ToString());

        return baselines;
    }
}
