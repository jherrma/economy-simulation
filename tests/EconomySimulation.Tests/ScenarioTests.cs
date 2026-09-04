using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/09-01.</summary>
public sealed class ScenarioTests
{
    private static string Directory => Path.Combine(Repo.Root, "config", "scenarios");

    /// <summary>
    /// What each scenario is allowed to touch, written here rather than derived from the files.
    ///
    /// This is the guard against the slow failure: a scenario that picks up an unrelated override
    /// over time and turns the comparison into a two-variable experiment nobody remembers
    /// designing. Deriving the expectation from the file would make the test agree with whatever
    /// the file happened to say.
    /// </summary>
    private static readonly Dictionary<string, string[]> Claims = new(StringComparer.Ordinal)
    {
        ["credit_off"] = [],
        ["credit_low"] = ["credit.credit_enabled", "credit.theta_max", "credit.theta_min"],
        ["credit_high"] = ["credit.credit_enabled", "credit.theta_max", "credit.theta_min"],
        ["credit_high_no_money_creation"] =
            ["credit.credit_enabled", "credit.money_creation", "credit.theta_max", "credit.theta_min"],
        ["credit_high_willingness_rationing"] =
            ["credit.credit_enabled", "credit.theta_max", "credit.theta_min", "prices.rationing"],
    };

    private static IReadOnlyList<Scenario> Load()
    {
        var all = Scenario.All(Directory);

        Assert.True(
            all.IsSuccess,
            "Expected the five scenarios to load: " + string.Join("; ", all.Errors.Select(e => e.Message)));

        return all.Value;
    }

    private static Scenario Named(string name) => Load().Single(s => s.Name == name);

    // ---- the five ------------------------------------------------------------------------------

    [Fact]
    public void TheFiveScenariosOfSection9_AreCommittedAndLoad()
    {
        Assert.Equal(Scenario.Names, [.. Load().Select(s => s.Name)]);
    }

    /// <summary>
    /// The control arm is literally the defaults. If this ever fails, every paired comparison in the
    /// campaign has silently stopped being a comparison against the specification's baseline.
    /// </summary>
    [Fact]
    public void CreditOff_IsTheDefaults()
    {
        var baseline = Named("credit_off");

        Assert.Empty(baseline.Overrides);
        Assert.Equal(SimulationParameters.Default, baseline.Parameters);
    }

    [Theory]
    [InlineData("credit_off")]
    [InlineData("credit_low")]
    [InlineData("credit_high")]
    [InlineData("credit_high_no_money_creation")]
    [InlineData("credit_high_willingness_rationing")]
    public void EachScenario_ClaimsExactlyWhatItIsFor(string name)
    {
        Assert.Equal((IEnumerable<string>)Claims[name], Named(name).Overrides);
    }

    /// <summary>
    /// Nothing moves that the file did not ask to move. The reverse is allowed and deliberate:
    /// `credit_low` pins a theta that currently equals the default, so it claims a key it does not
    /// change, which is what stops it following the default if the default ever moves.
    /// </summary>
    [Theory]
    [InlineData("credit_off")]
    [InlineData("credit_low")]
    [InlineData("credit_high")]
    [InlineData("credit_high_no_money_creation")]
    [InlineData("credit_high_willingness_rationing")]
    public void EachScenario_ChangesNothingItDoesNotClaim(string name)
    {
        var scenario = Named(name);

        Assert.Empty(scenario.Changes.Except(scenario.Overrides, StringComparer.Ordinal));
    }

    [Fact]
    public void CreditLow_PinsAThetaItDoesNotYetChange()
    {
        var low = Named("credit_low");

        Assert.Contains("credit.theta_max", low.Overrides);
        Assert.DoesNotContain("credit.theta_max", low.Changes);
    }

    /// <summary>
    /// The variants are `credit_high` plus one thing. There is no include mechanism, so the three
    /// shared lines are copied — and this is what stops the copies drifting apart, which is the
    /// failure the story was written against.
    /// </summary>
    [Theory]
    [InlineData("credit_high_no_money_creation", "credit.money_creation")]
    [InlineData("credit_high_willingness_rationing", "prices.rationing")]
    public void EachVariant_DiffersFromCreditHighInOneKey(string name, string key)
    {
        var high = Named("credit_high").Parameters;
        var variant = Named(name).Parameters;

        Assert.Equal([key, "run.scenario"], [.. variant.DifferencesFrom(high)]);
    }

    // ---- the id --------------------------------------------------------------------------------

    [Theory]
    [InlineData("credit_off")]
    [InlineData("credit_high")]
    public void TheScenarioId_ComesFromTheFilename(string name)
    {
        Assert.Equal(name, Named(name).Parameters.Run.Scenario);
    }

    [Fact]
    public void AScenarioThatNamesItself_IsRefused()
    {
        var refused = Scenario.FromToml("credit_high", "[run]\nscenario = \"credit_low\"\n");

        Assert.True(refused.IsFailed);
        Assert.Contains("run.scenario", refused.Errors[0].Message, StringComparison.Ordinal);
    }

    // ---- laid on top of something other than the defaults ------------------------------------

    /// <summary>
    /// The property the whole overlay exists for: a caller that has already changed something keeps
    /// its change. `credit_high` never mentions `loan_rate` or `k`, so a probe sweeping either one
    /// must still be sweeping it after the scenario is applied — and if this ever stopped holding,
    /// every sensitivity in §10.4 would silently have been measured at the default.
    /// </summary>
    [Fact]
    public void ApplyTo_KeepsWhatTheCallerAlreadyChanged()
    {
        var basis = SimulationParameters.Default with
        {
            Prices = SimulationParameters.Default.Prices with { K = 0.2 },
            Credit = SimulationParameters.Default.Credit with { LoanRate = new Rate(13.0) },
        };

        var applied = Named("credit_high").ApplyTo(basis);

        Assert.True(applied.IsSuccess);
        Assert.Equal(0.2, applied.Value.Prices.K);
        Assert.Equal(13.0, applied.Value.Credit.LoanRate.PercentPerAnnum);
        Assert.True(applied.Value.Credit.CreditEnabled);
        Assert.Equal(0.4, applied.Value.Credit.ThetaMin);
    }

    /// <summary>The same for the tables, which the loader rebuilds rather than copies.</summary>
    [Fact]
    public void ApplyTo_KeepsAModifiedCategoryTable()
    {
        var basis = SimulationParameters.Default.WithHouseholds(500);

        var applied = Named("credit_high").ApplyTo(basis);

        Assert.True(applied.IsSuccess);
        Assert.Equal(500, applied.Value.Run.Households);
        Assert.Equal(basis.Categories, applied.Value.Categories);
    }

    [Fact]
    public void ApplyTo_StillNamesTheRun()
    {
        var applied = Named("credit_low").ApplyTo(SimulationParameters.Default.WithHouseholds(500));

        Assert.True(applied.IsSuccess);
        Assert.Equal("credit_low", applied.Value.Run.Scenario);
    }

    /// <summary>
    /// Loading a scenario onto the defaults and applying it to the defaults are the same thing.
    /// They are two code paths and this is the one place they have to agree.
    /// </summary>
    [Theory]
    [InlineData("credit_off")]
    [InlineData("credit_high_willingness_rationing")]
    public void ApplyTo_TheDefaults_IsTheScenarioItself(string name)
    {
        var scenario = Named(name);

        Assert.Equal(scenario.Parameters, scenario.ApplyTo(SimulationParameters.Default).Value);
    }

    // ---- the id on every row -------------------------------------------------------------------

    /// <summary>
    /// End to end: the name on the file becomes the name on every output row. The writer's own
    /// tests set the id by hand, which cannot catch a scenario that never got its name.
    /// </summary>
    [Fact]
    public void TheScenarioId_ReachesEveryOutputRow()
    {
        var directory = Path.Combine(Path.GetTempPath(), "economy-simulation-scenario-" + Guid.NewGuid().ToString("N"));

        try
        {
            var scenario = Named("credit_high_no_money_creation");
            var simulation = new Simulation(scenario.Parameters, runSeed: 3);

            Assert.True(simulation.Start().IsSuccess);

            using (var writer = MetricsWriter.Create(simulation, directory).Value)
            {
                for (var tick = 1; tick <= 2; tick++)
                {
                    Assert.True(simulation.RunTick(tick).IsSuccess);
                    Assert.True(writer.Write(simulation).IsSuccess);
                }

                Assert.True(writer.Finish().IsSuccess);
            }

            foreach (var file in new[] { "run.csv", "tiers.csv" })
            {
                var csv = Csv.Read(Path.Combine(directory, file));

                Assert.NotEqual(0, csv.RowCount);
                Assert.All(csv.Rows(), r => Assert.Equal(scenario.Name, csv.Text(r, "scenario")));
            }
        }
        finally
        {
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, recursive: true);
            }
        }
    }

    // ---- the schema ----------------------------------------------------------------------------

    /// <summary>
    /// A typo fails rather than silently running the baseline. This is 02-02's asymmetry, inherited
    /// by scenarios for free because a scenario file is an ordinary partial configuration.
    /// </summary>
    [Fact]
    public void AMisspelledKey_IsRefused()
    {
        var refused = Scenario.FromToml("credit_high", "[credit]\ncredit_enabled = true\ntheta_maxx = 0.9\n");

        Assert.True(refused.IsFailed);
        Assert.Contains("credit.theta_maxx", refused.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownSection_IsRefused()
    {
        var refused = Scenario.FromToml("credit_high", "[kredit]\ncredit_enabled = true\n");

        Assert.True(refused.IsFailed);
    }

    [Fact]
    public void AnAbsentKey_Defaults()
    {
        var scenario = Scenario.FromToml("credit_high", "[credit]\ncredit_enabled = true\n");

        Assert.True(scenario.IsSuccess);
        Assert.Equal(SimulationParameters.Default.Prices.K, scenario.Value.Parameters.Prices.K);
    }

    [Fact]
    public void AMissingScenarioFile_FailsTheWholeSet()
    {
        var missing = Scenario.All(Path.Combine(Repo.Root, "config"));

        Assert.True(missing.IsFailed);
        Assert.Equal(Scenario.Names.Count, missing.Errors.Count);
    }
}
