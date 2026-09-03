using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Gates;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/08-02. V3: multiply every nominal quantity by `c` and nothing real may move.
///
/// The committed gate runs the real configuration over eight seeds and 360 ticks. These tests are
/// about the two things that cannot be seen from a passing run: that the scaling itself is exactly
/// what V3 specifies, and that the gate fails when it should.
/// </summary>
public sealed class NeutralityGateTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    // ---- what scales and what does not ---------------------------------------------------------

    /// <summary>Every nominal parameter is exactly `c` times what it was, at both scale factors, with no cent lost.</summary>
    [Theory]
    [InlineData(2.0)]
    [InlineData(0.5)]
    public void EveryNominalParameterScalesExactly(double scale)
    {
        var scaled = NeutralityGate.Scale(Defaults, scale);

        Assert.Equal(Defaults.Income.MeanIncome.Cents * scale, scaled.Income.MeanIncome.Cents);
        Assert.Equal(Defaults.Prices.PriceFloor.Cents * scale, scaled.Prices.PriceFloor.Cents);

        for (var c = 0; c < Defaults.Categories.Count; c++)
        {
            Assert.Equal(Defaults.Categories[c].PriceRef.Cents * scale, scaled.Categories[c].PriceRef.Cents);
        }

        // The derived quantities V3 names, which no line of the gate sets by hand.
        Assert.Equal(Defaults.M0.Cents * scale, scaled.M0.Cents);
        Assert.Equal(Defaults.OpeningPool.Cents * scale, scaled.OpeningPool.Cents);
        Assert.Equal(Defaults.OpeningHouseholdCash.Cents * scale, scaled.OpeningHouseholdCash.Cents);
    }

    /// <summary>
    /// `a_g` is a floor in euros per tick, so it scales. `b_g` is a coefficient on income, so it
    /// must not: scaling it too double-counts and produces a *near*-neutral result, which is worse
    /// than an obviously broken one because it reads as noise.
    /// </summary>
    [Theory]
    [InlineData(2.0)]
    [InlineData(0.5)]
    public void TheStoneGearyFloorScalesAndTheSlopeDoesNot(double scale)
    {
        var scaled = NeutralityGate.Scale(Defaults, scale);

        for (var c = 0; c < Defaults.Categories.Count; c++)
        {
            var was = Defaults.Categories[c];
            var now = scaled.Categories[c];

            // To the cent: `a_g` is derived, and `necessity · v · mean_income` need not be an even
            // number of cents — clothing's 3575 is not — so halving it is half a cent out. That
            // half cent is the same one the gate's control run exists to calibrate.
            Assert.InRange(now.Floor(scaled.Income.MeanIncome).Cents - (was.Floor(Defaults.Income.MeanIncome).Cents * scale), -0.5, 0.5);
            Assert.Equal(was.IncomeSlope, now.IncomeSlope);
        }
    }

    /// <summary>Everything V3 calls a pure number is untouched: λ, σ, the shares, the multipliers, `k`, the loan rate, θ.</summary>
    [Theory]
    [InlineData(2.0)]
    [InlineData(0.5)]
    public void PureNumbersAreLeftAlone(double scale)
    {
        var scaled = NeutralityGate.Scale(Defaults, scale);

        Assert.Equal(Defaults.Decision, scaled.Decision);
        Assert.Equal(Defaults.Credit, scaled.Credit);
        Assert.Equal(Defaults.Prices.K, scaled.Prices.K);
        Assert.Equal(Defaults.Tiers, scaled.Tiers);
        Assert.Equal(Defaults.Income.SigmaIncome, scaled.Income.SigmaIncome);
        Assert.Equal(Defaults.Income.OpeningCashShare, scaled.Income.OpeningCashShare);

        for (var c = 0; c < Defaults.Categories.Count; c++)
        {
            Assert.Equal(Defaults.Categories[c].V, scaled.Categories[c].V);
            Assert.Equal(Defaults.Categories[c].Necessity, scaled.Categories[c].Necessity);
            Assert.Equal(Defaults.Categories[c].Capacity, scaled.Categories[c].Capacity);
        }
    }

    /// <summary>The control is one cent, and only on the mean income. Everything else about it is the baseline.</summary>
    [Fact]
    public void TheControlIsOneCentOnMeanIncome()
    {
        var control = NeutralityGate.OneCent(Defaults);

        Assert.Equal(Defaults.Income.MeanIncome.Cents + 1, control.Income.MeanIncome.Cents);
        Assert.Equal(Defaults with { Income = Defaults.Income }, control with { Income = Defaults.Income });
    }

    // ---- reading the output back ----------------------------------------------------------------

    /// <summary>
    /// What a column measures is read off how the writer formatted it: two decimals is money, six
    /// is a pure number, none is a count or a label.
    ///
    /// Not from a list of column names, because the schema is additive: a gate keyed by name would
    /// silently stop checking every column added after it was written, which for this project means
    /// it stops checking the newest work first.
    /// </summary>
    [Theory]
    [InlineData("650.00", Quantity.Money)]
    [InlineData("-1.50", Quantity.Money)]
    [InlineData("1.040561", Quantity.Dimensionless)]
    [InlineData("0.000000", Quantity.Dimensionless)]
    [InlineData("42", Quantity.Count)]
    [InlineData("-3", Quantity.Count)]
    [InlineData("food", Quantity.Text)]
    [InlineData("credit_off", Quantity.Text)]
    public void AColumnsKindIsReadOffItsFormatting(string value, Quantity expected) =>
        Assert.Equal(expected, OutputFile.KindOf(value));

    /// <summary>The price indices are the one kind named rather than read: nothing in the format of a ratio says what it is a ratio of.</summary>
    [Fact]
    public void ThePriceIndicesAreNamed()
    {
        Assert.True(OutputFile.IsPriceIndex("cpi"));
        Assert.True(OutputFile.IsPriceIndex("cpi_food"));
        Assert.False(OutputFile.IsPriceIndex("abstainer_quality"));
        Assert.False(OutputFile.IsPriceIndex("mix_share"));
    }

    /// <summary>
    /// The window means skip the warm-up, and the shelf series are kept apart by category and tier:
    /// one mean over all eighteen shelves' prices would be a number with no meaning.
    /// </summary>
    [Fact]
    public void TheWindowSkipsTheWarmupAndKeepsTheShelvesApart()
    {
        using var workspace = Workspace.Create("test-window");

        var directory = workspace.Arm("run");
        var run = Runs.Execute(Small, seed: 1, directory);
        Assert.True(run.IsSuccess, run.IsFailed ? run.Errors[0].Message : "");

        var window = WindowMeans.Of(directory);
        var tick = new WindowMeans.Series("run.csv", "tick");

        // Ticks 31 to 90 are the measured window; their mean tick number is 60.5.
        Assert.Equal(60.5, window.Mean(tick), 9);
        Assert.True(window.Has(new WindowMeans.Series("tiers.csv", "food.budget.price")));
        Assert.True(window.Has(new WindowMeans.Series("tiers.csv", "appliances.premium.sold")));
        Assert.Equal(Quantity.Money, window.Kind(new WindowMeans.Series("tiers.csv", "food.budget.price")));
        Assert.Equal(Quantity.PriceIndex, window.Kind(new WindowMeans.Series("run.csv", "cpi")));
    }

    // ---- the gate can fail ---------------------------------------------------------------------

    /// <summary>
    /// A `price_floor` left in old money, and the gate says so.
    ///
    /// This is the likeliest first failure of V3 in any implementation of this model, and at the
    /// default of one euro it is also invisible: prices run from one to sixteen hundred euros and
    /// nothing ever reaches the floor, so leaving it unscaled changes nothing at all. The test
    /// therefore prices the floor where it binds — a hundred and fifty euros, above leisure's
    /// opening budget price of a hundred and twenty — which is the only configuration in which the mistake has a
    /// consequence, and exactly the configuration in which it would otherwise be found by accident.
    /// </summary>
    [Fact]
    public void AnUnscaledPriceFloorIsCaught()
    {
        using var workspace = Workspace.Create("test-unscaled-floor");

        var report = NeutralityGate.Run(BindingFloor, [1, 2, 3, 4], workspace, scalePriceFloor: false);

        Assert.False(report.Passed, report.ToString());
    }

    /// <summary>And with the floor scaled, that same configuration is neutral again — so the failure above is the floor and not the floor's value.</summary>
    [Fact]
    public void TheSameConfigurationIsNeutralOnceTheFloorScales()
    {
        using var workspace = Workspace.Create("test-scaled-floor");

        var report = NeutralityGate.Run(BindingFloor, [1, 2, 3, 4], workspace);

        Assert.True(report.Passed, report.ToString());
    }

    /// <summary>
    /// A town of two hundred over ninety ticks. Small enough for a test session, long enough to
    /// have a measured window at all — which the gate's whole comparison is taken over. The shelves
    /// are restocked for two hundred, not for a thousand: capacity is derived, not chosen.
    /// </summary>
    private static readonly SimulationParameters Small = Defaults.WithHouseholds(200) with
    {
        Run = Defaults.Run with { Households = 200, Ticks = 90, WarmupTicks = 30 },
    };

    /// <summary>
    /// The same town with a floor that binds: a hundred and fifty euros, above leisure's opening
    /// budget price of a hundred and twenty.
    ///
    /// It is a fixture, not a scenario. A binding floor means a shelf that cannot clear, so goods
    /// go unsold, households hoard what they could not spend, and the pool drains — at twelve
    /// months it empties around tick 65 and the run halts on its own calibration check. The large
    /// opening pool is there to let the run finish, not because the economy is a sensible one.
    /// </summary>
    private static readonly SimulationParameters BindingFloor = Small with
    {
        Prices = Small.Prices with { PriceFloor = Money.FromEuros(150) },
        Money = Small.Money with { OpeningPoolMonths = 240 },
    };
}
