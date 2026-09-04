using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/05-01. Three buckets, and the one that must not be demand.</summary>
public sealed class DemandAccountingTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static readonly GoodsTable Goods = new(Defaults);

    private const int Food = 0;
    private const int Budget = 0;
    private const int Standard = 1;
    private const int Premium = 2;

    /// <summary>
    /// `D = sold + blocked`. **`unaffordable` appears in neither.** Asserted by name because both
    /// ways of getting it wrong are silent: fold unaffordable in and prices rise on goods nobody
    /// can buy, making more households unable to buy them; leave blocked out and demand can never
    /// exceed supply, so prices only ever fall.
    /// </summary>
    [Fact]
    public void DemandIsSoldPlusBlocked_AndUnaffordableIsInNeither()
    {
        var market = new Market(Goods);

        market.Sell(Food, Budget);
        market.Sell(Food, Budget);
        market.Sell(Food, Budget);
        market.RecordBlocked(Food, Budget);
        market.RecordBlocked(Food, Budget);

        Assert.Equal(5, market.Demand(Food, Budget));

        for (var i = 0; i < 50; i++)
        {
            market.RecordUnaffordable(Food, Budget);
        }

        Assert.Equal(50, market.Unaffordable(Food, Budget));
        Assert.Equal(5, market.Demand(Food, Budget));
        Assert.Equal(3, market.Sold(Food, Budget));
        Assert.Equal(2, market.Blocked(Food, Budget));
    }

    /// <summary>A willing household with no cash leaves the shelf's demand exactly where it was.</summary>
    [Fact]
    public void AWillingButBrokeHousehold_LeavesDemandUnchanged()
    {
        var market = new Market(Goods);
        var population = Households.Specified(Goods, [Money.FromEuros(650)], [1.0]);
        var books = Ledger.Open([Money.Zero], Money.FromEuros(1_000_000));
        var walker = new Walker(Defaults, Goods, market, population, books, new LoanBook(population.Count, 1), runSeed: 1);
        population.RefreshWant(0, Food, 1);

        var before = market.Demand(Food, Budget);
        Assert.True(walker.Run(1).IsSuccess);

        Assert.Equal(1, market.Unaffordable(Food, Budget));
        Assert.Equal(before, market.Demand(Food, Budget));
    }

    /// <summary>`blocked > 0` implies the shelf ended the tick empty, and `sold ≤ units`, on every shelf.</summary>
    [Fact]
    public void BlockedImpliesAnEmptyShelf_AndSoldNeverExceedsSupply()
    {
        var simulation = new Simulation(Defaults, runSeed: 5);
        var sawBlocked = false;

        for (var tick = 1; tick <= 10; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);

            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                for (var t = 0; t < Goods.TierCount; t++)
                {
                    Assert.True(simulation.Market.Sold(c, t) <= Goods.Units(c, t));

                    if (simulation.Market.Blocked(c, t) > 0)
                    {
                        sawBlocked = true;
                        Assert.Equal(0, simulation.Market.Stock(c, t));
                        Assert.Equal(Goods.Units(c, t), simulation.Market.Sold(c, t));
                    }
                }
            }
        }

        Assert.True(sawBlocked);
    }

    /// <summary>
    /// Counters are per tier and are never aggregated to the category before repricing: a
    /// shortage at budget and a surplus at premium in the same category move the two prices in
    /// opposite directions. That relative movement is the trade-down channel.
    /// </summary>
    [Fact]
    public void CountersArePerTier_SoRelativeTierPricesCanMove()
    {
        var market = new Market(Goods);
        var budgetBefore = market.Price(Food, Budget);
        var standardBefore = market.Price(Food, Standard);
        var premiumBefore = market.Price(Food, Premium);

        for (var i = 0; i < Goods.Units(Food, Budget); i++)
        {
            market.Sell(Food, Budget);
        }

        market.RecordBlocked(Food, Budget);
        market.RecordBlocked(Food, Budget);

        for (var i = 0; i < Goods.Units(Food, Standard); i++)
        {
            market.Sell(Food, Standard); // exactly cleared: no signal
        }

        market.Reprice(Defaults.Prices.K, Defaults.Prices.PriceFloor);

        Assert.True(market.Price(Food, Budget) > budgetBefore);
        Assert.Equal(standardBefore, market.Price(Food, Standard));
        Assert.True(market.Price(Food, Premium) < premiumBefore);
    }

    /// <summary>The three counters are per shelf, and the shelf count is eighteen. Output (07-01) writes all three.</summary>
    [Fact]
    public void AllThreeCountersExistForEveryShelf()
    {
        var market = new Market(Goods);

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            for (var t = 0; t < Goods.TierCount; t++)
            {
                Assert.Equal(0, market.Sold(c, t));
                Assert.Equal(0, market.Blocked(c, t));
                Assert.Equal(0, market.Unaffordable(c, t));
            }
        }

        Assert.Equal(18, Goods.ShelfCount);
    }
}
