using System.Reflection;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Gates;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/10-03. **V5a**: the identity archetype table reproduces the pre-archetype model
/// byte for byte.
///
/// As with V5, most of these are about the fixture rather than about the model, because a fixture is
/// the one kind of test that rots silently. This one has an extra property to defend: it is never
/// regenerated. When it goes red the cheap move is to take a new photograph, and that move destroys
/// the only evidence that the mechanism is neutral when off.
/// </summary>
public sealed class ArchetypeGateTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static readonly int[] OneSeed = [1];

    // ---- the claim itself -------------------------------------------------------------------

    /// <summary>
    /// The committed fixture, taken before E10 existed, against the engine as it stands. If this
    /// fails, either the identity table is not the identity or the archetype code is perturbing a
    /// run it should not touch.
    /// </summary>
    [Fact]
    public void TheCommittedFixtureStillMatches()
    {
        using var workspace = Workspace.Create("test-archetypes-committed");

        var report = ArchetypeGate.Run(Defaults, Runs.ShortSeeds, workspace);

        Assert.True(report.Passed, report.ToString());
    }

    /// <summary>
    /// Both arms, and the projection, said out loud. A gate that compares a subset of the output
    /// without saying which subset is a gate whose green is not a claim.
    /// </summary>
    [Fact]
    public void TheGateStatesWhatItCompares()
    {
        using var workspace = Workspace.Create("test-archetypes-projection");

        var report = ArchetypeGate.Run(Defaults, OneSeed, workspace, Fresh(workspace, OneSeed));
        var against = Fixture(report);

        Assert.True(report.Passed, report.ToString());

        Assert.Contains(against.Observations, o => o.Contains("compared whole: run.csv, tiers.csv, run.done", StringComparison.Ordinal));
        Assert.Contains(against.Observations, o => o.Contains("not compared: effective-config.toml", StringComparison.Ordinal));
        Assert.Contains(against.Observations, o => o.Contains("columns the fixture and the run have in common", StringComparison.Ordinal));
        Assert.Contains(against.Observations, o => o.Contains("credit_off: 1 seeds", StringComparison.Ordinal));
        Assert.Contains(against.Observations, o => o.Contains("credit_high: 1 seeds", StringComparison.Ordinal));

        // And the fixture says what it came from, including the commit.
        Assert.Contains(against.Observations, o => o.Contains("f4bd9758585af4ea77392f27b12297d36d02e5ce", StringComparison.Ordinal));
    }

    /// <summary>
    /// A table that is not the identity is refused before its output is believed — and the
    /// comparison then fails too, which is the report a person wants: the table moved, and here is
    /// the first line of output that moved with it.
    ///
    /// A one-type table cannot be tilted, and that turns out to be a property worth naming rather
    /// than a nuisance: with a single type at share 1.0 the column scale *is* that type's weight, so
    /// the loader divides it straight back to 1.0. **The identity table is a fixed point of
    /// normalisation, and it is the only one-type table there is.** Making the model move takes a
    /// real population — two types who want different things — which is what this uses.
    /// </summary>
    [Fact]
    public void ATiltedTableIsRefusedAndTheOutputMoves()
    {
        using var workspace = Workspace.Create("test-archetypes-tilted");

        var tilted = ConfigurationLoader.FromToml(
            """
            [archetypes.hungry]
            share = 0.5
            w = { food = 1.10 }
            [archetypes.frugal]
            share = 0.5
            w = { food = 0.90 }
            """);

        Assert.True(tilted.IsSuccess, string.Join("; ", tilted.Errors.Select(e => e.Message)));
        Assert.False(tilted.Value.Archetypes.IsIdentity);

        var report = ArchetypeGate.Run(tilted.Value, OneSeed, workspace, Fresh(workspace, OneSeed));

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(report.Checks[0].Failures, f => f.Contains("not the identity table", StringComparison.Ordinal));
        Assert.Contains(Fixture(report).Failures, f => f.Contains("different parameters", StringComparison.Ordinal));
        Assert.Contains(Fixture(report).Failures, f => f.Contains("archetypes.", StringComparison.Ordinal));
    }

    /// <summary>
    /// The fixed point, on its own account: normalising a one-type table returns the identity
    /// whatever the author wrote, so a configuration with one archetype is v1 by construction and
    /// not by anybody remembering to write 1.0 six times.
    /// </summary>
    [Fact]
    public void AOneTypeTableNormalisesBackToTheIdentity()
    {
        var loaded = ConfigurationLoader.FromToml(
            """
            [archetypes.whatever]
            share = 1.0
            w = { food = 1.05, hobby = 0.4, electronics = 12.0 }
            """);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));
        Assert.All(loaded.Value.Archetypes.Types[0].W, w => Assert.Equal(1.0, w.Value));

        // And so it is the identity, whatever it is called: the name is a label and no part of the
        // model reads it.
        Assert.True(loaded.Value.Archetypes.IsIdentity);
    }

    // ---- the fixture ---------------------------------------------------------------------------

    /// <summary>A fixture that has moved is reported with the file, the line and the column.</summary>
    [Fact]
    public void AFixtureTheModelHasMovedAwayFromIsReported()
    {
        using var workspace = Workspace.Create("test-archetypes-moved");

        var fixture = Fresh(workspace, OneSeed);
        var run = Path.Combine(Runs.Directory(Path.Combine(fixture, "credit_high"), 1), "run.csv");
        var lines = File.ReadAllLines(run);

        // One cent on one cell of one row of one seed.
        lines[2] = lines[2].Replace(",0.00,", ",0.01,", StringComparison.Ordinal);
        File.WriteAllLines(run, lines);

        var report = ArchetypeGate.Run(Defaults, OneSeed, workspace, fixture);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Fixture(report).Failures, f => f.Contains("credit_high, seed 1: run.csv", StringComparison.Ordinal));
    }

    /// <summary>
    /// **A failing comparison does not repair the fixture, and there is no command that would.**
    ///
    /// The second half is the part worth asserting. V5 has a deliberate `rebaseline`, because the
    /// model is expected to move and a baseline of a moved model is a legitimate thing to want. V5a
    /// has none: it compares against a moment in history, and a moment in history does not get
    /// retaken.
    /// </summary>
    [Fact]
    public void AFailingComparisonLeavesTheFixtureAlone_AndNothingCanRegenerateIt()
    {
        using var workspace = Workspace.Create("test-archetypes-no-self-repair");

        var fixture = Fresh(workspace, OneSeed);
        var run = Path.Combine(Runs.Directory(Path.Combine(fixture, "credit_off"), 1), "run.csv");
        var tampered = File.ReadAllText(run).Replace(",0.00,", ",0.01,", StringComparison.Ordinal);

        File.WriteAllText(run, tampered);

        var report = ArchetypeGate.Run(Defaults, OneSeed, workspace, fixture);

        Assert.False(report.Passed);
        Assert.Equal(tampered, File.ReadAllText(run));

        Assert.DoesNotContain(
            typeof(ArchetypeGate).GetMethods(BindingFlags.Public | BindingFlags.Static),
            m => m.Name.Contains("Rebaseline", StringComparison.OrdinalIgnoreCase)
                 || m.Name.Contains("Regenerate", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A seed with no fixture is reported rather than quietly skipped.</summary>
    [Fact]
    public void AMissingSeedIsReportedRatherThanSkipped()
    {
        using var workspace = Workspace.Create("test-archetypes-missing-seed");

        var fixture = Fresh(workspace, OneSeed);
        Directory.Delete(Runs.Directory(Path.Combine(fixture, "credit_off"), 1), recursive: true);

        var report = ArchetypeGate.Run(Defaults, OneSeed, workspace, fixture);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Fixture(report).Failures, f => f.Contains("no fixture for seed 1", StringComparison.Ordinal));
    }

    /// <summary>A fixture nobody has taken is a failure, not a pass. An absent comparison must never read as agreement.</summary>
    [Fact]
    public void AMissingFixtureIsAFailure()
    {
        using var workspace = Workspace.Create("test-archetypes-no-fixture");

        var report = ArchetypeGate.Run(Defaults, OneSeed, workspace, Path.Combine(workspace.Root, "nothing-here"));

        Assert.False(report.Passed);
        Assert.Contains(Fixture(report).Failures, f => f.Contains("not generated by this gate", StringComparison.Ordinal));
    }

    /// <summary>
    /// The stored configuration is not compared as text — a pre-E10 file has no `[archetypes]`
    /// section — but it still has to *describe* the run: loaded through the current schema, it must
    /// resolve to exactly the parameters being run. That is what says an absent section resolves to
    /// the identity table.
    /// </summary>
    [Fact]
    public void AFixtureTakenUnderDifferentParametersIsRefused()
    {
        using var workspace = Workspace.Create("test-archetypes-other-parameters");

        var fixture = Fresh(workspace, OneSeed);
        var elsewhere = Defaults with { Decision = Defaults.Decision with { Lambda = 1.05 } };
        var report = ArchetypeGate.Run(elsewhere, OneSeed, workspace, fixture);

        Assert.False(report.Passed, report.ToString());
        Assert.Contains(Fixture(report).Failures, f => f.Contains("different parameters", StringComparison.Ordinal));
        Assert.Contains(Fixture(report).Failures, f => f.Contains("decision.lambda", StringComparison.Ordinal));
    }

    /// <summary>
    /// And the pre-E10 configuration really does have no archetypes section — otherwise the check
    /// above would be passing for the wrong reason.
    /// </summary>
    [Fact]
    public void TheStoredConfigurationPredatesTheArchetypesSection()
    {
        var stored = Path.Combine(
            ArchetypeGate.BaselineDirectory,
            "credit_off",
            "seed-1",
            "effective-config.toml");

        var text = File.ReadAllText(stored);

        Assert.DoesNotContain("[archetypes", text, StringComparison.Ordinal);

        var loaded = ConfigurationLoader.FromToml(text);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));
        Assert.True(loaded.Value.Archetypes.IsIdentity);
    }

    // ---- the shared-column rule ---------------------------------------------------------------

    /// <summary>
    /// The rest of the projection: a file the fixture and the run share is compared on the columns
    /// they have in common, so a column a later story adds does not turn this gate permanently red
    /// while saying nothing about neutrality — and a column they *do* share still has to agree.
    /// </summary>
    [Fact]
    public void SharedColumnsAreComparedAndNewOnesAreCounted()
    {
        using var workspace = Workspace.Create("test-archetypes-shared-columns");

        var before = workspace.Arm("before");
        var after = workspace.Arm("after");

        File.WriteAllText(Path.Combine(before, "cohorts.csv"), "tick,spend\n1,10\n2,20\n");
        File.WriteAllText(Path.Combine(after, "cohorts.csv"), "tick,spend,archetype\n1,10,average\n2,20,average\n");

        Assert.Null(Comparison.FirstDifferenceOnSharedColumns(before, after, "cohorts.csv", out var dropped));
        Assert.Equal(1, dropped);

        File.WriteAllText(Path.Combine(after, "cohorts.csv"), "tick,spend,archetype\n1,10,average\n2,21,average\n");

        var difference = Comparison.FirstDifferenceOnSharedColumns(before, after, "cohorts.csv", out _);

        Assert.NotNull(difference);
        Assert.Equal("spend", difference.Column);
        Assert.Equal(3, difference.Row);
    }

    /// <summary>Columns are matched by name, not by position: a reordered header is not a difference.</summary>
    [Fact]
    public void SharedColumnsAreMatchedByName()
    {
        using var workspace = Workspace.Create("test-archetypes-column-order");

        var before = workspace.Arm("before");
        var after = workspace.Arm("after");

        File.WriteAllText(Path.Combine(before, "cohorts.csv"), "tick,spend\n1,10\n");
        File.WriteAllText(Path.Combine(after, "cohorts.csv"), "spend,tick\n10,1\n");

        Assert.Null(Comparison.FirstDifferenceOnSharedColumns(before, after, "cohorts.csv", out var dropped));
        Assert.Equal(0, dropped);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static GateCheck Fixture(GateReport report) =>
        report.Checks.First(c => c.Name.Contains("pre-archetype fixture", StringComparison.Ordinal));

    /// <summary>
    /// A copy of the committed fixture, so that staging a stale one does not touch the repository's.
    ///
    /// Copied rather than generated: there is no command that generates this fixture, which is the
    /// point of it.
    /// </summary>
    private static string Fresh(Workspace workspace, IReadOnlyList<int> seeds)
    {
        var fixture = Path.Combine(workspace.Root, "fixture");

        Directory.CreateDirectory(fixture);

        File.Copy(
            Path.Combine(ArchetypeGate.BaselineDirectory, ArchetypeGate.ProvenanceFile),
            Path.Combine(fixture, ArchetypeGate.ProvenanceFile));

        foreach (var arm in ArchetypeGate.Arms)
        {
            foreach (var seed in seeds)
            {
                var from = Runs.Directory(Path.Combine(ArchetypeGate.BaselineDirectory, arm), seed);
                var into = Runs.Directory(Path.Combine(fixture, arm), seed);

                Directory.CreateDirectory(into);

                foreach (var file in Directory.EnumerateFiles(from))
                {
                    File.Copy(file, Path.Combine(into, Path.GetFileName(file)));
                }
            }
        }

        return fixture;
    }
}
