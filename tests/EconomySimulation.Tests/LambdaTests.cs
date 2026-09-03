using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See `01-SIMULATION.md` §5.3: the reservation price on money, `λ_h = λ · min(1, φ / b_h)`.</summary>
public sealed class LambdaTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private const int Food = 0;
    private const int Premium = 2;

    /// <summary>A household on the given income wanting food, with the given cash, walks once; returns the food tier it lands on.</summary>
    private static int FoodTierWithCash(int cashEuros, SimulationParameters parameters, int incomeEuros = 650)
    {
        var goods = new GoodsTable(parameters);
        var market = new Market(goods);
        var population = Households.Specified(goods.CategoryCount, [Money.FromEuros(incomeEuros)], [1.0]);
        var books = Ledger.Open([Money.FromEuros(cashEuros)], Money.FromEuros(1_000_000));
        var walker = new Walker(parameters, goods, market, population, books, runSeed: 1);
        population.RefreshWant(0, Food, 1);

        var tier = -1;
        walker.Observer = (int h, in Candidate c, WalkOutcome o) =>
        {
            if (o == WalkOutcome.Taken && c.Category == Food)
            {
                tier = Math.Max(tier, c.Tier);
            }
        };

        Assert.True(walker.Run(1).IsSuccess);
        return tier;
    }

    /// <summary>
    /// Below φ months of cash, λ is the plain λ: the median household takes standard food and not
    /// premium (0.672 &lt; 1), exactly as the ladder table says, with one month or two of cash.
    /// </summary>
    [Theory]
    [InlineData(650)]
    [InlineData(1300)]
    public void BelowTheBuffer_LambdaIsUnchanged(int cash)
    {
        Assert.Equal(2.0, Defaults.Decision.BufferMonths);
        Assert.Equal(1, FoodTierWithCash(cash, Defaults));
    }

    /// <summary>
    /// Above it, λ falls in proportion: with two and a half months of cash λ_h = 0.8 and the
    /// premium step (0.672) still does not clear; with three months λ_h = 0.667 and it just does.
    /// </summary>
    [Fact]
    public void AboveTheBuffer_HoardedCashBuysQuality()
    {
        Assert.Equal(1, FoodTierWithCash(1625, Defaults));
        Assert.Equal(Premium, FoodTierWithCash(650 * 3, Defaults));
        Assert.Equal(Premium, FoodTierWithCash(650 * 4, Defaults));
    }

    /// <summary>`buffer_months = 0` switches the reservation price off: λ never moves, however much cash.</summary>
    [Fact]
    public void ZeroBufferMonths_SwitchesItOff()
    {
        var off = Defaults with { Decision = Defaults.Decision with { BufferMonths = 0.0 } };

        Assert.Equal(1, FoodTierWithCash(650 * 40, off));
    }

    /// <summary>
    /// The ratio is dimensionless: doubling income, cash and every price leaves the tier chosen
    /// unchanged, which is what nominal neutrality needs of it.
    /// </summary>
    [Fact]
    public void TheBufferIsDimensionless()
    {
        var doubled = Defaults with
        {
            Income = Defaults.Income with { MeanIncome = Defaults.Income.MeanIncome * 2 },
            Categories = Defaults.Categories.Select(c => c with { PriceRef = c.PriceRef * 2 }).ToArray(),
        };

        foreach (var months in new[] { 1, 3, 4, 8 })
        {
            Assert.Equal(
                FoodTierWithCash(650 * months, Defaults),
                FoodTierWithCash(1300 * months, doubled, incomeEuros: 1300));
        }
    }

    /// <summary>
    /// A poor household never has its λ raised: the rule only ever lowers it, so nobody skips a
    /// meal to build a buffer. On €300 with little cash, budget food is taken as before.
    /// </summary>
    [Fact]
    public void ThePoorAreNotAskedToSave()
    {
        Assert.Equal(0, FoodTierWithCash(300, Defaults, incomeEuros: 300));
        Assert.Equal(0, FoodTierWithCash(200, Defaults, incomeEuros: 300));
    }
}
