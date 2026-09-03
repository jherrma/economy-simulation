using EconomySimulation.Engine;
using EconomySimulation.Tests.Infrastructure;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/05-02. Eighteen prices, each finding its own level.</summary>
public sealed class RepriceTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    /// <summary>A pool that cannot drain in 360 ticks. See TheDefaultPoolIsExhaustedAtTickSixty.</summary>
    private static readonly SimulationParameters LongRun =
        Defaults with { Money = Defaults.Money with { OpeningPoolMonths = 400 } };

    private static readonly GoodsTable Goods = new(Defaults);

    private const int Food = 0;
    private const int Budget = 0;
    private const int Standard = 1;
    private const int Premium = 2;

    // ---- the rule -------------------------------------------------------------------------------

    /// <summary>
    /// `price ← price · (1 + k · clamp((D − units) / units, −1, +1))`, per tier. A 20% shortage on
    /// standard food moves it 1%: 300 → 303. Half of budget unsold: 180 → 175.50. Nothing sold at
    /// premium: the clamp, 540 → 513.
    /// </summary>
    [Fact]
    public void TheRuleIsAppliedToEachTierOnItsOwnExcessDemand()
    {
        var market = new Market(Goods);
        var units = Goods.Units(Food, Standard);
        Assert.Equal(400, units);

        for (var i = 0; i < units; i++)
        {
            market.Sell(Food, Standard);
        }

        for (var i = 0; i < units / 5; i++)
        {
            market.RecordBlocked(Food, Standard);
        }

        for (var i = 0; i < Goods.Units(Food, Budget) / 2; i++)
        {
            market.Sell(Food, Budget);
        }

        market.Reprice(0.05, Defaults.Prices.PriceFloor);

        Assert.Equal(Money.FromEuros(303), market.Price(Food, Standard));
        Assert.Equal(Money.FromEuros(175.50m), market.Price(Food, Budget));
        Assert.Equal(Money.FromEuros(513), market.Price(Food, Premium));
    }

    [Fact]
    public void ExcessDemandIsClampedToPlusOrMinusOne()
    {
        var market = new Market(Goods);
        var units = Goods.Units(Food, Budget);

        for (var i = 0; i < units; i++)
        {
            market.Sell(Food, Budget);
        }

        for (var i = 0; i < 5 * units; i++)
        {
            market.RecordBlocked(Food, Budget); // D = 6 × units, clamped to +1
        }

        market.Reprice(0.05, Defaults.Prices.PriceFloor);

        Assert.Equal(Money.FromEuros(189), market.Price(Food, Budget)); // 180 · 1.05
    }

    /// <summary>Only the shelf with a signal moves. There is no category-wide adjustment.</summary>
    [Fact]
    public void EighteenTiersMoveIndependently()
    {
        var market = new Market(Goods);
        var before = Prices(market);

        for (var i = 0; i < Goods.CategoryCount; i++)
        {
            for (var t = 0; t < Goods.TierCount; t++)
            {
                for (var u = 0; u < Goods.Units(i, t); u++)
                {
                    market.Sell(i, t); // every shelf exactly cleared
                }
            }
        }

        market.RecordBlocked(3, Premium); // one shortage, on hobby premium

        market.Reprice(0.05, Defaults.Prices.PriceFloor);
        var after = Prices(market);

        for (var i = 0; i < after.Length; i++)
        {
            if (i == Goods.Index(3, Premium))
            {
                Assert.True(after[i] > before[i]);
            }
            else
            {
                Assert.Equal(before[i], after[i]);
            }
        }
    }

    // ---- timing ---------------------------------------------------------------------------------

    /// <summary>New prices take effect from the next tick: prices are constant through a walk.</summary>
    [Fact]
    public void PricesAreConstantThroughAWalk_AndMoveOnlyAtRepricing()
    {
        var simulation = new Simulation(Defaults, runSeed: 2);
        var atStart = Prices(simulation.Market);
        Money[]? atWalk = null;
        Money[]? beforeRepricing = null;

        simulation.StepObserver = step =>
        {
            if (step == TickStep.Walk)
            {
                atWalk = Prices(simulation.Market);
            }

            if (step == TickStep.Repricing)
            {
                beforeRepricing = Prices(simulation.Market);
            }
        };

        Assert.True(simulation.RunTick(1).IsSuccess);

        Assert.Equal(atStart, atWalk);
        Assert.Equal(atStart, beforeRepricing);
        Assert.NotEqual(atStart, Prices(simulation.Market));
    }

    // ---- the floor ------------------------------------------------------------------------------

    /// <summary>
    /// A shelf nobody buys from falls to `price_floor` and stops there, exactly. The floor is
    /// `Money`, so it scales with everything else under the neutrality test; a fixed number would
    /// break V3.
    /// </summary>
    [Fact]
    public void AnUnsoldShelfFallsToTheFloor_AndTheFloorIsMoney()
    {
        var market = new Market(Goods);
        var floor = Defaults.Prices.PriceFloor;
        Assert.IsType<Money>(floor);
        Assert.Equal(Money.FromEuros(1), floor);

        var last = market.Price(Food, Premium);

        for (var tick = 0; tick < 400; tick++)
        {
            market.Restock();
            market.Reprice(0.05, floor);

            var now = market.Price(Food, Premium);
            Assert.True(now <= last);
            Assert.True(now >= floor);
            last = now;
        }

        Assert.Equal(floor, market.Price(Food, Premium));
    }

    // ---- convergence ----------------------------------------------------------------------------

    /// <summary>
    /// A persistent 20% shortage on one shelf raises its price 1% per tick, monotonically, with
    /// no oscillation: after n ticks the price is 300 · 1.01ⁿ to the cent. The rounding does not
    /// compound, because the posted price is carried as a factor on the opening price.
    /// </summary>
    [Fact]
    public void APersistentTwentyPercentShortage_RaisesThePriceOnePercentPerTick()
    {
        var market = new Market(Goods);
        var units = Goods.Units(Food, Standard);
        var last = market.Price(Food, Standard);

        for (var n = 1; n <= 60; n++)
        {
            market.Restock();

            for (var i = 0; i < units; i++)
            {
                market.Sell(Food, Standard);
            }

            for (var i = 0; i < units / 5; i++)
            {
                market.RecordBlocked(Food, Standard);
            }

            market.Reprice(0.05, Defaults.Prices.PriceFloor);

            var now = market.Price(Food, Standard);
            var expected = Money.FromEuros(300).Scaled(Math.Pow(1.01, n));

            Assert.True(now > last, $"tick {n}: {now} not above {last}");
            Assert.InRange(now.Cents - expected.Cents, -1, 1);
            last = now;
        }
    }

    /// <summary>
    /// Premium starts in heavy surplus — at opening 40/40/20 supply meets a town that mostly wants
    /// budget or standard — and its price falls until enough households take the upgrade and the
    /// shelf clears, well inside the warm-up. Prices, not parameters, resolve the opening mix.
    /// </summary>
    [Fact]
    public void PremiumStartsInSurplus_AndClearsWithinTheWarmUp()
    {
        var simulation = new Simulation(LongRun, runSeed: 1);
        var opening = simulation.Market.Price(Food, Premium);
        Assert.Equal(Money.FromEuros(540), opening);

        var firstClearedAt = -1;
        var soldAtOpening = -1;
        var series = new List<Money>();

        for (var tick = 1; tick <= 120; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);
            series.Add(simulation.Market.Price(Food, Premium));

            if (tick == 1)
            {
                soldAtOpening = simulation.Market.Sold(Food, Premium);
            }

            if (firstClearedAt < 0 && simulation.Market.Sold(Food, Premium) == Goods.Units(Food, Premium))
            {
                firstClearedAt = tick;
            }
        }

        Assert.True(soldAtOpening < Goods.Units(Food, Premium) / 2, $"premium sold {soldAtOpening} of {Goods.Units(Food, Premium)} at opening");
        Assert.InRange(firstClearedAt, 1, 120);
        Assert.True(series[^1] < opening);

        // And it has settled: the last twenty ticks move far less than the first twenty did.
        var firstMove = Math.Abs(series[19].Cents - series[0].Cents);
        var lastMove = series.Skip(100).Max(p => p.Cents) - series.Skip(100).Min(p => p.Cents);
        Assert.True(lastMove < firstMove / 4, $"premium food still moving: {lastMove} cents over the last 20 ticks against {firstMove} over the first 20");
    }

    /// <summary>The realised tier mix is recorded per shelf and appears nowhere in the parameters.</summary>
    [Fact]
    public void TheTierMixIsRecorded_NeverConfigured()
    {
        var schema = File.ReadAllText(Path.Combine(Repo.Root, "src", "EconomySimulation.Engine", "Configuration", "Sections.cs"));

        Assert.DoesNotContain("TierMix", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tier_mix", schema, StringComparison.OrdinalIgnoreCase);

        var simulation = new Simulation(Defaults, runSeed: 1);
        Assert.True(simulation.RunTick(1).IsSuccess);

        var mix = Enumerable.Range(0, Goods.TierCount).Select(t => simulation.Market.Sold(Food, t)).ToArray();
        Assert.True(mix.Sum() > 0);
    }

    // ---- the finding ----------------------------------------------------------------------------

    /// <summary>
    /// **A known calibration failure, pinned so that it is not forgotten.** With the price rule
    /// running, the default twelve-month pool is exhausted at about tick 60 and the run halts in
    /// the income step. This is not the transient: with a pool that cannot drain, the leak
    /// settles at about €135k a tick — a fifth of income — and never stops (next test). See
    /// `01-SIMULATION.md` §7.2. When the specification is amended to anchor nominal output to
    /// nominal income, this test must be inverted into V4's "the pool has stopped falling".
    /// </summary>
    [Fact]
    public void TheDefaultPoolIsExhaustedAtTickSixty_AKnownCalibrationFailure()
    {
        var simulation = new Simulation(Defaults, runSeed: 1);
        var run = simulation.Run();

        Assert.True(run.IsFailed);
        Assert.Contains("calibration result", run.Errors[0].Message, StringComparison.Ordinal);
        Assert.InRange(simulation.Tick, 40, 80);
    }

    /// <summary>
    /// The leak is structural, not transient: from tick 200 to 360 the pool falls by more than a
    /// tenth of income every tick, while prices have converged and shelves clear. Nothing anchors
    /// the price level, so nominal output settles below nominal income and cash accumulates in
    /// every income group. See `01-SIMULATION.md` §7.2.
    /// </summary>
    [Fact]
    public void TheDrainIsStructural_NotTransient()
    {
        var simulation = new Simulation(LongRun, runSeed: 1);
        var income = Money.Zero;
        for (var h = 0; h < simulation.Population.Count; h++)
        {
            income += simulation.Population.Income[h];
        }

        for (var tick = 1; tick <= 200; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);
        }

        var poolAt200 = simulation.Books.Pool;

        for (var tick = 201; tick <= 360; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);
        }

        var perTick = (poolAt200 - simulation.Books.Pool).Cents / 160.0;
        Assert.True(perTick > 0.10 * income.Cents, $"pool fell {perTick / 100:0} a tick against income {income.ToCsv()}");
    }

    private static Money[] Prices(Market market)
    {
        var prices = new Money[Goods.GoodCount];

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            for (var t = 0; t < Goods.TierCount; t++)
            {
                prices[Goods.Index(c, t)] = market.Price(c, t);
            }
        }

        return prices;
    }
}
