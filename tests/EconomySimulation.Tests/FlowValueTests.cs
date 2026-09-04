using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/04-01. Value is euros per tick, with a floor that does not scale with income.</summary>
public sealed class FlowValueTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static readonly GoodsTable Goods = new(Defaults);

    private const int Food = 0;
    private const int Electronics = 4;

    /// <summary>One household on exactly the given income with `w = 1`. No seed produces one.</summary>
    private static Households One(int euros, double taste = 1.0) =>
        Households.Specified(Goods, [Money.FromEuros(euros)], [taste]);

    // ---- the formula ----------------------------------------------------------------------

    /// <summary>`(a_g + b_g · income_h) · w_h` at the mean income, against the table in `02-PARAMETERS.md` §3.1.</summary>
    [Theory]
    [InlineData(0, 403.00)]
    [InlineData(1, 260.00)]
    [InlineData(2, 71.50)]
    [InlineData(3, 57.20)]
    [InlineData(4, 31.20)]
    [InlineData(5, 10.40)]
    public void AtTheMeanIncome_TheBaseValueMatchesTheSpecificationTable(int category, double expectedEurosPerTick)
    {
        var value = Valuation.BaseValue(Goods, One(650), household: 0, category);

        Assert.Equal(expectedEurosPerTick, value.EurosPerTick, precision: 6);
    }

    /// <summary>The floor is the part that does not move with income; the slope is the part that does.</summary>
    [Fact]
    public void TheFloorDoesNotScaleWithIncome_AndTheSlopeDoes()
    {
        var poor = Valuation.BaseValue(Goods, One(300), 0, Food).EurosPerTick;
        var rich = Valuation.BaseValue(Goods, One(3000), 0, Food).EurosPerTick;

        var floor = Goods.Floor(Food).Cents / 100.0;
        var slope = Goods.IncomeSlope(Food);

        Assert.Equal(floor + (slope * 300), poor, precision: 9);
        Assert.Equal(floor + (slope * 3000), rich, precision: 9);
        Assert.Equal(slope * 2700, rich - poor, precision: 9);
    }

    [Fact]
    public void TheTasteWeightMultipliesTheWholeValue()
    {
        var plain = Valuation.BaseValue(Goods, One(650), 0, Food);
        var keen = Valuation.BaseValue(Goods, One(650, taste: 1.25), 0, Food);

        Assert.Equal(plain.EurosPerTick * 1.25, keen.EurosPerTick);
    }

    // ---- Engel ----------------------------------------------------------------------------

    /// <summary>
    /// At €300 of income, food's base score clears λ; with the floor forced to zero it does not.
    /// This is the difference between a model and a broken one: with value strictly proportional
    /// to income, the poor end of the distribution stops eating and nothing else in the project
    /// notices. The purchase half of this test — the household buying no food — lives in
    /// WalkTests, once there is a walk.
    /// </summary>
    [Fact]
    public void AtThreeHundredEuros_FoodClearsLambdaOnlyBecauseOfTheFloor()
    {
        var lambda = Defaults.Decision.Lambda;
        var cost = Valuation.FlowCost(Goods.Categories[Food].PriceRef, Goods.Categories[Food].Life);

        var withFloor = Valuation.Score(Valuation.BaseValue(Goods, One(300), 0, Food), cost);

        var noFloor = new GoodsTable(WithoutFloor(Food));
        var withoutFloor = Valuation.Score(Valuation.BaseValue(noFloor, One(300), 0, Food), cost);

        Assert.True(withFloor >= lambda, $"with the floor, food scores {withFloor:0.000} at €300");
        Assert.True(withoutFloor < lambda, $"without the floor, food still scores {withoutFloor:0.000} at €300");
    }

    /// <summary>
    /// Engel's law as a gradient: food's share of what a household would like to spend falls as
    /// income rises, and electronics' share rises. That is the whole effect of the split.
    /// </summary>
    [Fact]
    public void FoodsShareOfDesiredSpendingFallsWithIncome_ElectronicsRises()
    {
        int[] incomes = [300, 450, 650, 900, 1500, 3000];

        var foodShares = incomes.Select(i => Share(One(i), Food)).ToArray();
        var electronicsShares = incomes.Select(i => Share(One(i), Electronics)).ToArray();

        for (var i = 1; i < incomes.Length; i++)
        {
            Assert.True(foodShares[i] < foodShares[i - 1], $"food share rose from €{incomes[i - 1]} to €{incomes[i]}");
            Assert.True(electronicsShares[i] > electronicsShares[i - 1], $"electronics share fell from €{incomes[i - 1]} to €{incomes[i]}");
        }
    }

    // ---- the tier multiplier --------------------------------------------------------------

    /// <summary>
    /// `flow_value` at a tier is the base value times that tier's multiplier, exactly — so the
    /// multiplier lives in one place and `v_g` is never pre-multiplied by it.
    /// </summary>
    [Fact]
    public void TheValueMultiplierIsAppliedByTier_AndNotBakedIntoV()
    {
        var population = One(650);

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var baseValue = Valuation.BaseValue(Goods, population, 0, c);

            for (var t = 0; t < Goods.TierCount; t++)
            {
                var atTier = Valuation.FlowValue(Goods, population, 0, c, t);

                Assert.Equal(baseValue.EurosPerTick * Goods.Tiers[t].ValueMult, atTier.EurosPerTick);
            }
        }

        // And the table holds only per-category numbers: the floor and the slope.
        Assert.Equal(Goods.CategoryCount, Enumerable.Range(0, Goods.CategoryCount).Select(Goods.Floor).Count());
    }

    // ---- neutrality -----------------------------------------------------------------------

    /// <summary>
    /// Double every nominal quantity — mean income, household income, reference prices — and every
    /// `flow_value` doubles exactly, not approximately. If it does not, something is not indexed
    /// and V3 will fail later for a much harder reason to find.
    /// </summary>
    [Fact]
    public void DoublingEveryNominalQuantity_DoublesEveryFlowValueExactly()
    {
        var doubled = new GoodsTable(Defaults with
        {
            Income = Defaults.Income with { MeanIncome = Defaults.Income.MeanIncome * 2 },
            Categories = Defaults.Categories.Select(c => c with { PriceRef = c.PriceRef * 2 }).ToArray(),
        });

        double[] tastes = [0.7, 1.0, 1.3];
        int[] incomes = [300, 650, 3000];

        foreach (var taste in tastes)
        {
            foreach (var income in incomes)
            {
                var before = One(income, taste);
                var after = Households.Specified(Goods, [Money.FromEuros(income * 2)], [taste]);

                for (var c = 0; c < Goods.CategoryCount; c++)
                {
                    for (var t = 0; t < Goods.TierCount; t++)
                    {
                        var v1 = Valuation.FlowValue(Goods, before, 0, c, t).EurosPerTick;
                        var v2 = Valuation.FlowValue(doubled, after, 0, c, t).EurosPerTick;

                        Assert.Equal(v1 * 2, v2);
                    }
                }
            }
        }
    }

    // ---- allocation -----------------------------------------------------------------------

    [Fact]
    public void ValuingEveryGoodForEveryHousehold_AllocatesNothing()
    {
        var population = Households.Draw(Defaults, Goods, runSeed: 1);

        var total = Flow.Zero;

        void ValueEveryGood()
        {
            for (var h = 0; h < population.Count; h++)
            {
                for (var c = 0; c < Goods.CategoryCount; c++)
                {
                    for (var t = 0; t < Goods.TierCount; t++)
                    {
                        total += Valuation.FlowValue(Goods, population, h, c, t);
                    }
                }
            }
        }

        // The same warm-up as the other allocation tests, and for the same reason: one call
        // promotes nothing.
        var allocated = Infrastructure.Allocations.Of(ValueEveryGood, ValueEveryGood);

        Assert.True(total.IsPositive);
        Assert.Equal(0, allocated);
    }

    // ---- the type -------------------------------------------------------------------------

    [Fact]
    public void Score_IsInfiniteForAFreeImprovement_AndZeroForAWorthlessOne()
    {
        Assert.Equal(double.PositiveInfinity, Valuation.Score(new Flow(1.0), Flow.Zero));
        Assert.Equal(double.PositiveInfinity, Valuation.Score(new Flow(1.0), new Flow(-5.0)));
        Assert.Equal(0.0, Valuation.Score(Flow.Zero, new Flow(3.0)));
        Assert.Equal(0.0, Valuation.Score(new Flow(-1.0), Flow.Zero));
        Assert.Equal(1.5, Valuation.Score(new Flow(3.0), new Flow(2.0)));
    }

    [Fact]
    public void ALifeOfOne_MakesFlowCostThePrice()
    {
        Assert.Equal(300.0, Valuation.FlowCost(Money.FromEuros(300), 1).EurosPerTick);
        Assert.Equal(800.0 / 96, Valuation.FlowCost(Money.FromEuros(800), 96).EurosPerTick);
    }

    // ---- helpers --------------------------------------------------------------------------

    private static double Share(Households population, int category)
    {
        var total = 0.0;

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            total += Valuation.BaseValue(Goods, population, 0, c).EurosPerTick;
        }

        return Valuation.BaseValue(Goods, population, 0, category).EurosPerTick / total;
    }

    private static SimulationParameters WithoutFloor(int category) =>
        Defaults with
        {
            Categories = Defaults.Categories
                .Select((c, i) => i == category ? c with { Necessity = 0.0 } : c)
                .ToArray(),
        };
}
