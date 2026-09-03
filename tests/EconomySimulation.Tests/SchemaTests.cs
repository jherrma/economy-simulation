using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// The schema against the specification, parameter by parameter. See spec/stories/02-01.
///
/// These tests read `spec/02-PARAMETERS.md` and compare it with the defaults in the code. That is
/// the whole point: a test holding its own copy of the numbers would pass just as happily while
/// the file and the model drifted apart, which is the failure this project cannot afford — the
/// specification is what the results are defended with.
/// </summary>
public sealed class SchemaTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    // ---- §1 population and run -----------------------------------------------------------

    [Fact]
    public void RunParameters_MatchTheSpecification()
    {
        var spec = SpecFile.Defaults("## 1. Population and run");

        Assert.Equal(SpecFile.Integer(spec["households"]), Defaults.Run.Households);
        Assert.Equal(SpecFile.Number(spec["abstainer_share"]), Defaults.Run.AbstainerShare);
        Assert.Equal(SpecFile.Integer(spec["ticks"]), Defaults.Run.Ticks);
        Assert.Equal(SpecFile.Integer(spec["warmup_ticks"]), Defaults.Run.WarmupTicks);
        Assert.Equal(SpecFile.Integer(spec["seeds"]), Defaults.Run.Seeds);
    }

    // ---- §2 income ------------------------------------------------------------------------

    [Fact]
    public void IncomeParameters_MatchTheSpecification()
    {
        var spec = SpecFile.Defaults("## 2. Income");

        Assert.Equal(Money.FromEuros(650), Defaults.Income.MeanIncome);
        Assert.Equal(SpecFile.Number(spec["mean_income"]), Defaults.Income.MeanIncome.Cents / 100.0);
        Assert.Equal(SpecFile.Number(spec["σ_income"]), Defaults.Income.SigmaIncome);
        Assert.Equal(SpecFile.Number(spec["opening_cash_share"]), Defaults.Income.OpeningCashShare);
    }

    // ---- §3.1 categories -------------------------------------------------------------------

    [Fact]
    public void EveryCategoryRow_MatchesTheSpecification()
    {
        var spec = SpecFile.Table("### 3.1 Categories");

        // Names in the file are prose ("Hobby items", "Consumer electronics"); the schema uses
        // one word. The mapping is written out rather than guessed at.
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["food"] = "Food",
            ["leisure"] = "Leisure",
            ["clothing"] = "Clothing",
            ["hobby"] = "Hobby items",
            ["electronics"] = "Consumer electronics",
            ["appliances"] = "Appliances",
        };

        Assert.Equal(6, Defaults.Categories.Count);
        Assert.Equal(6, spec.Count);

        foreach (var category in Defaults.Categories)
        {
            var row = spec[byName[category.Name]];

            Assert.Equal(SpecFile.Integer(row[1]), category.Life);
            Assert.Equal(SpecFile.Integer(row[2]), category.Capacity);
            Assert.Equal(SpecFile.Number(row[3]), category.PriceRef.Cents / 100.0);
            Assert.Equal(SpecFile.Number(row[4]), category.V);
            Assert.Equal(SpecFile.Number(row[5]), category.Necessity);
            Assert.Equal(SpecFile.Flag(row[6]), category.Financeable);
            Assert.Equal(
                category.Financeable ? SpecFile.Integer(row[7]) : 0,
                category.Term);
        }
    }

    // ---- §3.2 tiers -------------------------------------------------------------------------

    [Fact]
    public void EveryTierRow_MatchesTheSpecification()
    {
        var spec = SpecFile.Table("### 3.2 Tiers");

        Assert.Equal(3, Defaults.Tiers.Count);

        foreach (var tier in Defaults.Tiers)
        {
            var row = spec[tier.Name];

            Assert.Equal(SpecFile.Number(row[1]), tier.PriceMult);
            Assert.Equal(SpecFile.Number(row[2]), tier.ValueMult);
            Assert.Equal(SpecFile.Number(row[3]), tier.UnitShare);
        }
    }

    // ---- §4, §5, §6 --------------------------------------------------------------------------

    [Fact]
    public void DecisionParameters_MatchTheSpecification()
    {
        var spec = SpecFile.Defaults("## 4. The decision");

        Assert.Equal(SpecFile.Number(spec["lambda"]), Defaults.Decision.Lambda);
        Assert.Equal(SpecFile.Number(spec["σ_w"]), Defaults.Decision.SigmaW);
        Assert.Equal(SpecFile.Number(spec["subsistence_share"]), Defaults.Decision.SubsistenceShare);
        Assert.Equal(SpecFile.Number(spec["buffer_months"]), Defaults.Decision.BufferMonths);
        Assert.Equal(
            spec["affordability_horizon"],
            Defaults.Decision.AffordabilityHorizon.ToTomlValue());
    }

    [Fact]
    public void CreditParameters_MatchTheSpecification()
    {
        var spec = SpecFile.Defaults("## 5. Credit");

        Assert.Equal("false", spec["credit_enabled"]);
        Assert.Equal(SpecFile.Number(spec["loan_rate"]), Defaults.Credit.LoanRate.PercentPerAnnum);
        Assert.Equal("true", spec["money_creation"]);
        Assert.Equal(SpecFile.Number(spec["theta_min"]), Defaults.Credit.ThetaMin);
        Assert.Equal(SpecFile.Number(spec["theta_max"]), Defaults.Credit.ThetaMax);
    }

    [Fact]
    public void PriceParameters_MatchTheSpecification()
    {
        var spec = SpecFile.Defaults("## 6. Prices");

        Assert.Equal(SpecFile.Number(spec["k"]), Defaults.Prices.K);
        Assert.Equal(SpecFile.Number(spec["price_floor"]), Defaults.Prices.PriceFloor.Cents / 100.0);
        Assert.Equal(spec["rationing"], Defaults.Prices.Rationing.ToTomlValue());
    }

    // ---- §7 money ----------------------------------------------------------------------------

    /// <summary>
    /// The three money quantities are derived here and written down there. If a derivation
    /// changes, this is what notices — and M0 is the right-hand side of V1, so nothing about it
    /// can be allowed to be approximately right.
    /// </summary>
    [Fact]
    public void TheOpeningMoneyStock_MatchesTheSpecification()
    {
        var spec = SpecFile.Table("## 7. Money");

        Assert.Equal(
            SpecFile.Number(spec["Opening household cash"][1]),
            Defaults.OpeningHouseholdCash.Cents / 100.0);

        Assert.Equal(
            SpecFile.Number(spec["Opening pool"][1]),
            Defaults.OpeningPool.Cents / 100.0);

        Assert.Equal(SpecFile.Number(spec["M0"][1]), Defaults.M0.Cents / 100.0);

        // And the arithmetic, in case the file and the code agree on a wrong sum.
        Assert.Equal(Money.FromEuros(650_000), Defaults.OpeningHouseholdCash);
        Assert.Equal(Money.FromEuros(7_800_000), Defaults.OpeningPool);
        Assert.Equal(Money.FromEuros(8_450_000), Defaults.M0);
        Assert.Equal(Defaults.OpeningHouseholdCash + Defaults.OpeningPool, Defaults.M0);
    }

    // ---- the disable-by-default rule ----------------------------------------------------------

    /// <summary>
    /// The rule that makes the baseline free. `credit_off` is not a scenario anyone configures;
    /// it is what the defaults give you, so the control arm cannot quietly drift away from the
    /// treatment arm's setup.
    /// </summary>
    [Fact]
    public void EveryBehaviourGate_DefaultsToDisabled()
    {
        Assert.False(Defaults.Credit.CreditEnabled);
    }

    /// <summary>
    /// `money_creation` is deliberately true, and it is not an exception to the rule above: it is
    /// not a gate on a behaviour but the choice between two ways of funding a loan, and it is
    /// inert while credit is off. It is asserted here so that reading the two together does not
    /// look like an oversight.
    /// </summary>
    [Fact]
    public void MoneyCreation_IsOnButInertWhileCreditIsOff()
    {
        Assert.True(Defaults.Credit.MoneyCreation);
        Assert.False(Defaults.Credit.CreditEnabled);
    }

    // ---- the schema can print itself ----------------------------------------------------------

    [Fact]
    public void TheSchemaPrintsEverySectionOfTheSpecification()
    {
        var toml = Defaults.ToToml();

        foreach (var section in
                 new[] { "[run]", "[income]", "[categories.food]", "[tiers.budget]", "[decision]", "[credit]", "[prices]", "[money]" })
        {
            Assert.Contains(section, toml, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Printed on a machine whose culture writes 0,35 for a third, the configuration would not
    /// parse back. Same reason Money formats invariantly.
    /// </summary>
    [Fact]
    public void ThePrintedConfiguration_IsInvariantCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");

            Assert.Contains("sigma_income = 0.35", Defaults.ToToml(), StringComparison.Ordinal);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}
