using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/07-02. The price level, and the mix that the price level hides.</summary>
public sealed class PriceIndexTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private const int Food = 0;
    private const int Budget = 0;
    private const int Standard = 1;
    private const int Premium = 2;

    private static (GoodsTable Goods, Market Market) Town(SimulationParameters? parameters = null)
    {
        var goods = new GoodsTable(parameters ?? Defaults);

        return (goods, new Market(goods));
    }

    /// <summary>`cpi_0 = 1` exactly — not to six decimals, exactly, because the two sums are the same integers.</summary>
    [Fact]
    public void AtTheOpeningPricesTheIndexIsExactlyOne()
    {
        var (goods, market) = Town();

        Assert.Equal(1.0, PriceIndex.Cpi(goods, market.Prices));

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            Assert.Equal(1.0, PriceIndex.ForCategory(goods, c, market.Prices));
        }
    }

    /// <summary>
    /// Scaling every price by `c` scales the index by exactly `c`. This is the nominal-neutrality
    /// half of V3 seen from the output side: the index is a ratio, so it carries the level and
    /// nothing else.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void ScalingEveryPriceScalesTheIndex(int factor)
    {
        var (goods, market) = Town();

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            for (var t = 0; t < goods.TierCount; t++)
            {
                market.SetPrice(c, t, market.Price(c, t) * factor);
            }
        }

        Assert.Equal(factor, PriceIndex.Cpi(goods, market.Prices));

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            Assert.Equal(factor, PriceIndex.ForCategory(goods, c, market.Prices));
        }
    }

    /// <summary>
    /// **The index is a price index, not a spending index.** Selling the entire town budget food
    /// instead of premium food does not move it by a hair, because the basket is the fixed unit
    /// supply and not what anyone bought. Confusing the two would hide the trade-down effect
    /// inside the headline number — a town that keeps prices flat by buying worse would read as
    /// unchanged, and that is the finding this project is looking for.
    /// </summary>
    [Fact]
    public void TheIndexIsInvariantToTheRealisedTierMix()
    {
        var (goods, market) = Town();
        var before = PriceIndex.Cpi(goods, market.Prices);

        for (var n = 0; n < goods.Units(Food, Budget); n++)
        {
            market.Sell(Food, Budget);
        }

        var after = PriceIndex.Cpi(goods, market.Prices);

        Assert.Equal(before, after);
        Assert.Equal(1.0, after);

        // The mix, meanwhile, moved all the way — which is the point of recording it beside the index.
        Assert.Equal(1.0, PriceIndex.MixShare(market, goods, Food, Budget));
        Assert.Equal(0.0, PriceIndex.MixShare(market, goods, Food, Premium));
    }

    /// <summary>The index weights each tier by its unit supply, so a premium price move counts for its 20% and no more.</summary>
    [Fact]
    public void TheBasketIsWeightedByUnitSupply()
    {
        var (goods, market) = Town();

        market.SetPrice(Food, Premium, market.Price(Food, Premium) * 2);

        var opening = 0L;

        for (var t = 0; t < goods.TierCount; t++)
        {
            opening += goods.OpeningPrice(Food, t).Cents * goods.Units(Food, t);
        }

        var added = goods.OpeningPrice(Food, Premium).Cents * (double)goods.Units(Food, Premium);

        Assert.Equal(1.0 + (added / opening), PriceIndex.ForCategory(goods, Food, market.Prices), 12);
    }

    /// <summary>The realised mix is a share of the category's sales, and it sums to one whenever anything sold.</summary>
    [Fact]
    public void TheMixIsAShareOfTheCategorysSales()
    {
        var (goods, market) = Town();

        market.Sell(Food, Budget);
        market.Sell(Food, Budget);
        market.Sell(Food, Standard);
        market.Sell(Food, Premium);

        Assert.Equal(0.5, PriceIndex.MixShare(market, goods, Food, Budget));
        Assert.Equal(0.25, PriceIndex.MixShare(market, goods, Food, Standard));
        Assert.Equal(0.25, PriceIndex.MixShare(market, goods, Food, Premium));

        // A category that sold nothing reports zero rather than a gap the reader has to interpret.
        Assert.Equal(0.0, PriceIndex.MixShare(market, goods, 5, Budget));
    }

    // ---- an output, never an input -----------------------------------------------------------

    /// <summary>
    /// Nothing in the model reads the index. Asserted the only way that is worth anything: the
    /// engine's own sources are scanned, and the decision, market and ledger code may not mention
    /// it at all.
    /// </summary>
    [Fact]
    public void NothingInTheModelReadsTheIndex()
    {
        var offenders = Infrastructure.Repo
            .EngineSources()
            .Where(p => !Path.GetFullPath(p).Contains(Path.Combine("Engine", "Output"), StringComparison.Ordinal))
            .Where(p => Path.GetFileName(p) != "Simulation.cs")
            .Where(p => File.ReadAllText(p).Contains("PriceIndex", StringComparison.Ordinal)
                || File.ReadAllText(p).Contains("Cpi", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(offenders.Length == 0, "the price index is an output; it turned up in: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// And in a run: the index moves with the prices the tick traded at, tick by tick, while the
    /// decisions that produced it never saw it.
    /// </summary>
    [Fact]
    public void TheRecordedIndexIsTheOneTheTickTradedAt()
    {
        var simulation = new Simulation(Defaults, runSeed: 1);
        Assert.True(simulation.Start().IsSuccess);

        Assert.True(simulation.RunTick(1).IsSuccess);
        Assert.Equal(1.0, simulation.Recorded.Cpi);

        for (var tick = 2; tick <= 10; tick++)
        {
            var traded = PriceIndex.Cpi(simulation.Goods, simulation.Market.Prices);
            Assert.True(simulation.RunTick(tick).IsSuccess);

            Assert.Equal(traded, simulation.Recorded.Cpi);
            Assert.NotEqual(1.0, simulation.Recorded.Cpi);
        }
    }
}
