using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/02-02.</summary>
public sealed class ConfigTests
{
    private static SimulationParameters Load(string toml)
    {
        var result = ConfigurationLoader.FromToml(toml);

        Assert.True(
            result.IsSuccess,
            "Expected this to load: " + string.Join("; ", result.Errors.Select(e => e.Message)));

        return result.Value;
    }

    private static IReadOnlyList<string> Problems(string toml)
    {
        var result = ConfigurationLoader.FromToml(toml);

        Assert.True(result.IsFailed, "Expected this to be rejected, and it loaded.");

        return [.. result.Errors.Select(e => e.Message)];
    }

    // ---- the committed fixture --------------------------------------------------------------

    /// <summary>
    /// config/default.toml round-trips to exactly the schema's defaults. Everything else in the
    /// project starts from that file, so if this holds, "the defaults" means one thing.
    /// </summary>
    [Fact]
    public void TheCommittedDefaultConfig_LoadsToTheSchemaDefaults()
    {
        var loaded = Load(File.ReadAllText(FixtureGenerator.DefaultConfigPath));

        Assert.Equal(SimulationParameters.Default, loaded);
    }

    [Fact]
    public void ThePrintedConfiguration_ReloadsToItself()
    {
        var once = Load(SimulationParameters.Default.ToToml());

        Assert.Equal(SimulationParameters.Default.ToToml(), once.ToToml());
    }

    // ---- absent versus unknown ---------------------------------------------------------------

    /// <summary>
    /// Absent means an older file, or a caller who does not care. It is filled from the default,
    /// which is what lets a scenario written today still run against a model that has grown three
    /// mechanisms.
    /// </summary>
    [Fact]
    public void AnEmptyFile_IsTheDefaultConfiguration()
    {
        Assert.Equal(SimulationParameters.Default, Load(""));
    }

    [Fact]
    public void AnOldFileMissingLaterKeys_StillRuns()
    {
        // A scenario file written before theta_min, money_creation or the tier table existed.
        var loaded = Load(
            """
            [run]
            households = 500
            ticks = 240
            warmup_ticks = 60

            [credit]
            credit_enabled = true
            """);

        Assert.Equal(500, loaded.Run.Households);
        Assert.Equal(240, loaded.Run.Ticks);
        Assert.True(loaded.Credit.CreditEnabled);

        // Everything it does not mention is the specification's default.
        Assert.Equal(SimulationParameters.Default.Credit.LoanRate, loaded.Credit.LoanRate);
        Assert.Equal(SimulationParameters.Default.Decision, loaded.Decision);
        Assert.Equal(SimulationParameters.Default.Tiers, loaded.Tiers);
    }

    /// <summary>
    /// Unknown means a typo, or a parameter someone invented. A misspelled key that is silently
    /// ignored produces a run at defaults that looks exactly like the run that was asked for —
    /// which is the worst kind of wrong result, because there is nothing to notice.
    /// </summary>
    [Fact]
    public void AMisspelledKey_IsRejected()
    {
        var problems = Problems(
            """
            [run]
            house_holds = 500
            """);

        Assert.Contains(problems, p => p.Contains("run.house_holds", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownSection_IsRejected()
    {
        var problems = Problems(
            """
            [housing]
            enabled = true
            """);

        Assert.Contains(problems, p => p.Contains("housing", StringComparison.Ordinal));
    }

    [Fact]
    public void AMisspelledKeyInsideACategory_IsRejected()
    {
        var problems = Problems(
            """
            [categories.food]
            lifetime = 2
            """);

        Assert.Contains(problems, p => p.Contains("categories.food.lifetime", StringComparison.Ordinal));
    }

    // ---- everything at once -------------------------------------------------------------------

    /// <summary>
    /// Three problems report as three. Returning on the first is the difference between one edit
    /// and a dozen run-fix-rerun cycles.
    /// </summary>
    [Fact]
    public void ThreeProblems_AllGetReported()
    {
        var problems = Problems(
            """
            [run]
            households = 0
            abstainer_share = 1.5

            [income]
            sigma_income = 0.0
            """);

        Assert.Contains(problems, p => p.StartsWith("run.households:", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("run.abstainer_share:", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("income.sigma_income:", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryProblemNamesTheKey_WhatWasExpected_AndWhatWasThere()
    {
        var problem = Problems(
            """
            [run]
            abstainer_share = 1.5
            """).Single();

        Assert.Equal("run.abstainer_share: expected a share in [0, 1], got 1.5", problem);
    }

    // ---- malformed input ----------------------------------------------------------------------

    [Fact]
    public void MalformedToml_IsAResult_NotAnException()
    {
        var result = ConfigurationLoader.FromToml("[run\nhouseholds = ");

        Assert.True(result.IsFailed);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void AValueOfTheWrongType_IsRejectedByName()
    {
        var problems = Problems(
            """
            [run]
            households = "a thousand"
            """);

        Assert.Contains(problems, p => p.StartsWith("run.households:", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnknownChoice_IsRejectedWithTheAllowedOnes()
    {
        var problems = Problems(
            """
            [prices]
            rationing = "auction"
            """);

        Assert.Contains(problems, p => p.Contains("random", StringComparison.Ordinal) && p.Contains("willingness", StringComparison.Ordinal));
    }

    [Fact]
    public void AMissingFile_IsAResult()
    {
        Assert.True(ConfigurationLoader.FromFile("/nowhere/at/all.toml").IsFailed);
    }

    // ---- range checks ---------------------------------------------------------------------------

    [Theory]
    [InlineData("[run]\nhouseholds = -1", "run.households")]
    [InlineData("[run]\nticks = 0", "run.ticks")]
    [InlineData("[run]\nseeds = 0", "run.seeds")]
    [InlineData("[run]\nabstainer_share = -0.1", "run.abstainer_share")]
    [InlineData("[income]\nsigma_income = -1.0", "income.sigma_income")]
    [InlineData("[income]\nopening_cash_share = 2.0", "income.opening_cash_share")]
    [InlineData("[decision]\nlambda = 0.0", "decision.lambda")]
    [InlineData("[decision]\nsigma_w = 0.0", "decision.sigma_w")]
    [InlineData("[decision]\nsubsistence_share = 1.2", "decision.subsistence_share")]
    [InlineData("[decision]\nbuffer_months = -1", "decision.buffer_months")]
    [InlineData("[credit]\nloan_rate = -1.0", "credit.loan_rate")]
    [InlineData("[credit]\ntheta_min = 0.9\ntheta_max = 0.1", "credit.theta_min")]
    [InlineData("[prices]\nk = 0.0", "prices.k")]
    [InlineData("[prices]\nprice_floor = 0.0", "prices.price_floor")]
    [InlineData("[money]\nopening_pool_months = 0", "money.opening_pool_months")]
    [InlineData("[categories.food]\nlife = 0", "categories.food.life")]
    [InlineData("[categories.food]\nv = 0.0", "categories.food.v")]
    [InlineData("[categories.food]\nnecessity = 1.5", "categories.food.necessity")]
    [InlineData("[categories.food]\nprice_ref = 0.0", "categories.food.price_ref")]
    [InlineData("[categories.hobby]\nterm = 0", "categories.hobby.term")]
    [InlineData("[categories.food]\nterm = 6", "categories.food.term")]
    public void OutOfRangeValues_AreRejectedByName(string toml, string key)
    {
        Assert.Contains(Problems(toml), p => p.StartsWith(key + ":", StringComparison.Ordinal));
    }

    // ---- cross-parameter checks -------------------------------------------------------------------

    [Fact]
    public void WarmupLongerThanTheRun_IsRejected()
    {
        var problems = Problems(
            """
            [run]
            ticks = 100
            warmup_ticks = 100
            """);

        Assert.Contains(problems, p => p.StartsWith("run.warmup_ticks:", StringComparison.Ordinal));
    }

    [Fact]
    public void TierUnitShares_MustSumToOne()
    {
        var problems = Problems(
            """
            [tiers.budget]
            unit_share = 0.5

            [tiers.standard]
            unit_share = 0.4

            [tiers.premium]
            unit_share = 0.2
            """);

        Assert.Contains(problems, p => p.StartsWith("tiers.unit_share:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The check the story is really about. A premium tier that is better value for money than
    /// standard inverts the ladder: every household jumps straight to premium, and the run
    /// produces numbers that look entirely fine.
    /// </summary>
    [Fact]
    public void ATierThatIsBetterValueThanTheOneBelowIt_IsRejected()
    {
        var problems = Problems(
            """
            [tiers.premium]
            price_mult = 1.8
            value_mult = 1.8
            """);

        Assert.Contains(problems, p => p.StartsWith("tiers.premium:", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("diminishing returns", StringComparison.Ordinal));
    }

    [Fact]
    public void ATierThatCostsMoreAndIsWorse_IsRejected()
    {
        var problems = Problems(
            """
            [tiers.premium]
            value_mult = 0.9
            """);

        Assert.Contains(problems, p => p.StartsWith("tiers.premium.value_mult:", StringComparison.Ordinal));
    }

    /// <summary>The specification's own tiers satisfy it: 1.133, then 0.800, then 0.500.</summary>
    [Fact]
    public void TheSpecifiedTiers_HaveDiminishingReturns()
    {
        var tiers = SimulationParameters.Default.Tiers;

        var ratios = new List<double>();
        for (var i = 0; i < tiers.Count; i++)
        {
            var deltaValue = i == 0 ? tiers[i].ValueMult : tiers[i].ValueMult - tiers[i - 1].ValueMult;
            var deltaPrice = i == 0 ? tiers[i].PriceMult : tiers[i].PriceMult - tiers[i - 1].PriceMult;
            ratios.Add(deltaValue / deltaPrice);
        }

        Assert.Equal(1.1333, ratios[0], 4);
        Assert.Equal(0.8000, ratios[1], 4);
        Assert.Equal(0.5000, ratios[2], 4);
        Assert.True(ratios[0] > ratios[1] && ratios[1] > ratios[2]);
    }

    // ---- a table overlays, it does not replace -----------------------------------------------------

    /// <summary>
    /// Naming one category changes that category. It does not hand you a one-category economy.
    ///
    /// This is worth its own test because the wrong behaviour does not fail: the run starts, the
    /// invariant holds, prices move, and the output is a perfectly consistent simulation of a
    /// town that eats and does nothing else.
    /// </summary>
    [Fact]
    public void NamingOneCategory_LeavesTheOtherFiveAlone()
    {
        var loaded = Load(
            """
            [categories.food]
            v = 0.7
            """);

        Assert.Equal(6, loaded.Categories.Count);
        Assert.Equal(0.7, loaded.Categories.Single(c => c.Name == "food").V);
        Assert.Equal(
            SimulationParameters.Default.Categories.Single(c => c.Name == "appliances"),
            loaded.Categories.Single(c => c.Name == "appliances"));
    }

    [Fact]
    public void NamingOneTier_LeavesTheOtherTwoAlone()
    {
        var loaded = Load(
            """
            [tiers.premium]
            price_mult = 2.0
            """);

        Assert.Equal(3, loaded.Tiers.Count);
        Assert.Equal(2.0, loaded.Tiers.Single(t => t.Name == "premium").PriceMult);
        Assert.Equal(
            SimulationParameters.Default.Tiers.Single(t => t.Name == "budget"),
            loaded.Tiers.Single(t => t.Name == "budget"));
    }

    /// <summary>Tiers come back in price order however the file was written.</summary>
    [Fact]
    public void TiersAreOrderedByPrice_NotByHowTheFileWasWritten()
    {
        var loaded = Load(
            """
            [tiers.luxury]
            price_mult = 3.0
            value_mult = 1.6
            unit_share = 0.0

            [tiers.premium]
            unit_share = 0.2
            """);

        Assert.Equal(["budget", "standard", "premium", "luxury"], loaded.Tiers.Select(t => t.Name));
    }

    // ---- capacity is derived, not chosen ---------------------------------------------------------

    /// <summary>
    /// capacity is round(households / life) by definition, so changing the population in a
    /// scenario does not mean recomputing six other numbers by hand.
    /// </summary>
    [Fact]
    public void ChangingTheHouseholdCount_RecomputesCapacity()
    {
        var loaded = Load(
            """
            [run]
            households = 500
            """);

        Assert.Equal(500, loaded.Categories.Single(c => c.Name == "food").Capacity);
        Assert.Equal(83, loaded.Categories.Single(c => c.Name == "clothing").Capacity);  // 500/6
        Assert.Equal(5, loaded.Categories.Single(c => c.Name == "appliances").Capacity); // 500/96
    }

    [Fact]
    public void ACapacityThatContradictsTheIdentity_IsRejected()
    {
        var problems = Problems(
            """
            [categories.food]
            capacity = 900
            """);

        Assert.Contains(problems, p => p.StartsWith("categories.food.capacity:", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("round(households / life)", StringComparison.Ordinal));
    }

    [Fact]
    public void ACapacityThatAgreesWithTheIdentity_IsAccepted()
    {
        Assert.Equal(1000, Load("[categories.food]\ncapacity = 1000").Categories.Single(c => c.Name == "food").Capacity);
    }

    /// <summary>
    /// The same identity for a configuration built in code, so that a gate or a test that wants a
    /// smaller town gets one rather than a town whose shelves were stocked for a larger one.
    ///
    /// It matters because the symptom is not obviously a mistake: at a fifth of the households and
    /// the same capacities, supply is five times demand, prices fall to the floor, households
    /// cannot spend what they earn, the pool drains and the run halts on its own calibration check
    /// around tick 60 — which reads as a finding about the parameters.
    /// </summary>
    [Fact]
    public void BuildingAConfigurationInCode_RecomputesCapacityToo()
    {
        var smaller = SimulationParameters.Default.WithHouseholds(200);

        Assert.Equal(200, smaller.Run.Households);
        Assert.Equal(200, smaller.Categories.Single(c => c.Name == "food").Capacity);
        Assert.Equal(33, smaller.Categories.Single(c => c.Name == "clothing").Capacity);  // 200/6
        Assert.Equal(2, smaller.Categories.Single(c => c.Name == "appliances").Capacity); // 200/96

        // And it agrees with what the loader would have derived, which is the point of there being
        // one definition rather than two.
        Assert.Equal(smaller, Load("[run]\nhouseholds = 200"));
    }

    // ---- the effective configuration is written out -------------------------------------------------

    [Fact]
    public void TheResolvedConfiguration_IsWrittenBesideTheOutput()
    {
        var directory = Path.Combine(Path.GetTempPath(), "economy-sim-" + Guid.NewGuid().ToString("N"));

        try
        {
            var parameters = Load("[run]\nhouseholds = 500");

            Assert.True(ConfigurationLoader.WriteEffectiveConfiguration(parameters, directory).IsSuccess);

            var written = File.ReadAllText(Path.Combine(directory, "effective-config.toml"));

            // What it actually ran with, not what the input file happened to mention: the input
            // said one thing, and the file records all of it.
            Assert.Contains("households = 500", written, StringComparison.Ordinal);
            Assert.Contains("capacity = 500", written, StringComparison.Ordinal);
            Assert.Contains("credit_enabled = false", written, StringComparison.Ordinal);
            Assert.Equal(parameters, Load(written));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
