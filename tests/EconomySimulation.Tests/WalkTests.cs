using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/04-05. The rule itself, and the hot loop.</summary>
public sealed class WalkTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private const int Food = 0;
    private const int Leisure = 1;
    private const int Budget = 0;
    private const int Standard = 1;

    private sealed record Event(int Tick, int Household, int Category, int Tier, double Score, Money DeltaPrice, WalkOutcome Outcome);

    /// <summary>Runs ticks with every outcome recorded.</summary>
    private static List<Event> Observe(Simulation simulation, int ticks, int from = 1)
    {
        var events = new List<Event>();
        var tick = 0;

        simulation.Shopping.Observer = (int h, in Candidate c, WalkOutcome o) =>
            events.Add(new Event(tick, h, c.Category, c.Tier, c.Score, c.DeltaPrice, o));

        for (tick = from; tick < from + ticks; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");
        }

        simulation.Shopping.Observer = null;
        return events;
    }

    // ---- the order --------------------------------------------------------------------------

    [Fact]
    public void HouseholdOrderIsASeededShuffle_RedrawnEveryTick()
    {
        var a = new Simulation(Defaults, runSeed: 7);
        var b = new Simulation(Defaults, runSeed: 7);
        var other = new Simulation(Defaults, runSeed: 8);

        Assert.True(a.RunTick(1).IsSuccess);
        var tickOne = a.Shopping.LastOrder.ToArray();
        Assert.True(a.RunTick(2).IsSuccess);
        var tickTwo = a.Shopping.LastOrder.ToArray();

        Assert.True(b.RunTick(1).IsSuccess);
        Assert.True(other.RunTick(1).IsSuccess);

        Assert.Equal(Enumerable.Range(0, Defaults.Run.Households), tickOne.OrderBy(h => h));
        Assert.NotEqual(tickOne, tickTwo);
        Assert.Equal(tickOne, b.Shopping.LastOrder.ToArray());
        Assert.NotEqual(tickOne, other.Shopping.LastOrder.ToArray());
    }

    [Fact]
    public void WillingnessRationing_VisitsKeenerHouseholdsFirst_AndIsNotTheDefault()
    {
        Assert.Equal(Rationing.Random, Defaults.Prices.Rationing);

        var simulation = new Simulation(
            Defaults with { Prices = Defaults.Prices with { Rationing = Rationing.Willingness } },
            runSeed: 7);
        Assert.True(simulation.RunTick(1).IsSuccess);

        var order = simulation.Shopping.LastOrder.ToArray();
        var tastes = order.Select(h => simulation.Population.TasteWeight[h]).ToArray();

        for (var i = 1; i < tastes.Length; i++)
        {
            Assert.True(tastes[i] <= tastes[i - 1], $"position {i} is keener than position {i - 1}");
        }
    }

    // ---- the rule -----------------------------------------------------------------------------

    /// <summary>
    /// Nothing below λ is ever acted on: the walk stops there. With the reservation price on money
    /// switched off, so that λ_h is λ for everyone; LambdaTests covers the per-household form.
    /// </summary>
    [Fact]
    public void NoCandidateBelowLambdaIsEverActedOn()
    {
        var plain = Defaults with { Decision = Defaults.Decision with { BufferMonths = 0.0 } };
        var events = Observe(new Simulation(plain, runSeed: 1), ticks: 3);

        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.True(e.Score >= Defaults.Decision.Lambda, $"{e}"));
    }

    /// <summary>An upgrade is taken only after the step below it, in the same walk.</summary>
    [Fact]
    public void AnUpgradeIsTakenOnlyAfterTheStepBelowIt()
    {
        var events = Observe(new Simulation(Defaults, runSeed: 1), ticks: 3);

        var upgrades = events.Where(e => e.Tier > 0).ToArray();
        Assert.NotEmpty(upgrades);

        foreach (var upgrade in upgrades)
        {
            var below = events.Where(e =>
                e.Tick == upgrade.Tick && e.Household == upgrade.Household
                && e.Category == upgrade.Category && e.Tier == upgrade.Tier - 1 && e.Outcome == WalkOutcome.Taken);

            Assert.Single(below);
        }

        // And an upgrade is acted on at all only when the step below was taken — a household
        // blocked or broke at budget never sees the standard step.
        var unmet = events.Where(e => e.Tier == 0 && e.Outcome != WalkOutcome.Taken).ToArray();
        Assert.NotEmpty(unmet);

        foreach (var e in unmet)
        {
            Assert.DoesNotContain(events, u =>
                u.Tick == e.Tick && u.Household == e.Household && u.Category == e.Category && u.Tier == 1);
        }
    }

    /// <summary>
    /// The sequential budget: a household on €400 of cash wanting food and leisure. Food's budget
    /// step (€180) and leisure's (€120) come first by score; the food upgrade (€120) then exceeds
    /// the €100 left, so it is unaffordable and the walk continues to the leisure upgrade (€80),
    /// which fits. Against a precomputed budget of €400 the food upgrade would have gone through.
    /// </summary>
    [Fact]
    public void TheBudgetDepletesSequentially()
    {
        var goods = new GoodsTable(Defaults);
        var market = new Market(goods);
        var population = Households.Specified(goods, [Money.FromEuros(650)], [1.0]);
        var books = Ledger.Open([Money.FromEuros(400)], Money.FromEuros(1_000_000));
        var walker = new Walker(Defaults, goods, market, population, books, new LoanBook(population.Count, 1), runSeed: 1);

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            population.RefreshWant(0, c, goods.Categories[c].Life);
        }

        var events = new List<(int Category, int Tier, WalkOutcome Outcome)>();
        walker.Observer = (int h, in Candidate c, WalkOutcome o) => events.Add((c.Category, c.Tier, o));

        Assert.True(walker.Run(1).IsSuccess);

        (int, int, WalkOutcome)[] expected =
        [
            (Food, Budget, WalkOutcome.Taken),           // 1.522, €180 → €220 left
            (Leisure, Budget, WalkOutcome.Taken),        // 1.473, €120 → €100 left
            (Food, Standard, WalkOutcome.Unaffordable),  // 1.075, €120 > €100
            (Leisure, Standard, WalkOutcome.Taken),      // 1.040, €80 → €20 left
        ];
        Assert.Equal(expected, events);

        Assert.Equal(Money.FromEuros(20), books.Cash(0));
        Assert.Equal(1, market.Sold(Food, Budget));
        Assert.Equal(0, market.Sold(Food, Standard));
        Assert.Equal(1, market.Sold(Leisure, Standard));
        Assert.Equal(0, market.Sold(Leisure, Budget));
        Assert.Equal(1, market.Unaffordable(Food, Standard));
        Assert.Equal(0, market.Blocked(Food, Standard));
    }

    /// <summary>Taking a candidate moves exactly its increment from the household to the pool.</summary>
    [Fact]
    public void TakingACandidateMovesTheIncrementToThePool()
    {
        var simulation = new Simulation(Defaults, runSeed: 2);
        var poolBefore = simulation.Books.Pool;
        var incomeTotal = Money.Zero;
        for (var h = 0; h < simulation.Population.Count; h++)
        {
            incomeTotal += simulation.Population.Income[h];
        }

        var events = Observe(simulation, ticks: 1);
        var takenTotal = events.Where(e => e.Outcome == WalkOutcome.Taken).Aggregate(Money.Zero, (sum, e) => sum + e.DeltaPrice);

        Assert.True(takenTotal > Money.Zero);
        Assert.Equal(takenTotal, simulation.Books.MovedFor(TransferReason.Purchase));
        Assert.Equal(poolBefore - incomeTotal + takenTotal, simulation.Books.Pool);

        // And the sum of increments is the sum of the prices posted during the walk — tick 1's are
        // the opening prices; repricing has since moved them — of the units that left the shelves.
        var soldValue = Money.Zero;
        for (var c = 0; c < simulation.Goods.CategoryCount; c++)
        {
            for (var t = 0; t < simulation.Goods.TierCount; t++)
            {
                soldValue += simulation.Goods.OpeningPrice(c, t) * simulation.Market.Sold(c, t);
            }
        }

        Assert.Equal(soldValue, takenTotal);
    }

    // ---- stock ----------------------------------------------------------------------------------

    /// <summary>
    /// Stock is consumed once per category, at the final tier: units sold in a category equal the
    /// households that bought in it, each household's unit is at the highest tier it took, and
    /// stock plus sold is the shelf's supply.
    /// </summary>
    [Fact]
    public void StockIsConsumedOnce_AtTheFinalTier()
    {
        var simulation = new Simulation(Defaults, runSeed: 3);
        var goods = simulation.Goods;
        var market = simulation.Market;
        var events = Observe(simulation, ticks: 1);

        var taken = events.Where(e => e.Outcome == WalkOutcome.Taken).ToArray();

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            var buyers = taken.Where(e => e.Category == c).GroupBy(e => e.Household).ToArray();
            var soldInCategory = Enumerable.Range(0, goods.TierCount).Sum(t => market.Sold(c, t));

            Assert.Equal(buyers.Length, soldInCategory);

            var finalTiers = buyers.Select(g => g.Max(e => e.Tier)).GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

            for (var t = 0; t < goods.TierCount; t++)
            {
                Assert.Equal(finalTiers.GetValueOrDefault(t), market.Sold(c, t));
                Assert.Equal(goods.Units(c, t), market.Stock(c, t) + market.Sold(c, t));
            }
        }

        // Some household walked budget → standard, so the distinction is exercised.
        Assert.Contains(taken.GroupBy(e => (e.Household, e.Category)), g => g.Count() > 1);
    }

    /// <summary>
    /// Blocked is recorded when a willing and able household finds the shelf empty, and the walk
    /// continues: at opening prices standard food is heavily oversubscribed, so a large part of
    /// the town is blocked there and keeps its budget unit.
    /// </summary>
    [Fact]
    public void ABlockedShelfIsRecorded_AndTheWalkContinues()
    {
        var simulation = new Simulation(Defaults, runSeed: 4);
        var market = simulation.Market;
        var events = Observe(simulation, ticks: 1);

        Assert.True(market.Blocked(Food, Standard) > 0);
        Assert.Equal(0, market.Stock(Food, Standard));

        var blockedAtStandard = events.Where(e => e.Category == Food && e.Tier == Standard && e.Outcome == WalkOutcome.Blocked).ToArray();
        Assert.Equal(market.Blocked(Food, Standard), blockedAtStandard.Length);

        // Each of them had taken budget, kept it, and went on to act on something else.
        foreach (var e in blockedAtStandard)
        {
            Assert.Contains(events, t => t.Household == e.Household && t.Category == Food && t.Tier == Budget && t.Outcome == WalkOutcome.Taken);
        }

        Assert.Contains(blockedAtStandard, e => events.Any(later => later.Household == e.Household && later.Category != Food));

        // blocked > 0 implies the shelf is empty, on every shelf.
        for (var c = 0; c < simulation.Goods.CategoryCount; c++)
        {
            for (var t = 0; t < simulation.Goods.TierCount; t++)
            {
                if (market.Blocked(c, t) > 0)
                {
                    Assert.Equal(0, market.Stock(c, t));
                }
            }
        }
    }

    /// <summary>A household with no cash is unaffordable everywhere, buys nothing, and blocks nothing.</summary>
    [Fact]
    public void ABrokeHouseholdIsUnaffordable_NotBlocked()
    {
        var goods = new GoodsTable(Defaults);
        var market = new Market(goods);
        var population = Households.Specified(goods, [Money.FromEuros(650)], [1.0]);
        var books = Ledger.Open([Money.Zero], Money.FromEuros(1_000_000));
        var walker = new Walker(Defaults, goods, market, population, books, new LoanBook(population.Count, 1), runSeed: 1);
        population.RefreshWant(0, Food, 1);
        population.RefreshWant(0, Leisure, 1);

        Assert.True(walker.Run(1).IsSuccess);

        Assert.Equal(1, market.Unaffordable(Food, Budget));
        Assert.Equal(1, market.Unaffordable(Leisure, Budget));
        Assert.Equal(0, market.Blocked(Food, Budget));
        Assert.Equal(0, market.Sold(Food, Budget));
        Assert.True(population.Wanted[population.AgeIndex(0, Food)]);
    }

    /// <summary>
    /// A higher tier that has repriced below the one under it is a free improvement: the increment
    /// is negative, the household is refunded the difference, and it has paid the premium price in
    /// total. Nothing in the ledger is disturbed.
    /// </summary>
    [Fact]
    public void ANegativeIncrementIsRefunded_AndTheTotalPaidIsThePostedPrice()
    {
        var goods = new GoodsTable(Defaults);
        var market = new Market(goods);
        market.SetPrice(Food, 2, Money.FromEuros(250)); // premium below standard's 300

        var population = Households.Specified(goods, [Money.FromEuros(650)], [1.0]);
        var books = Ledger.Open([Money.FromEuros(1000)], Money.FromEuros(1_000_000));
        var walker = new Walker(Defaults, goods, market, population, books, new LoanBook(population.Count, 1), runSeed: 1);
        population.RefreshWant(0, Food, 1);

        Assert.True(walker.Run(1).IsSuccess);

        Assert.Equal(1, market.Sold(Food, 2));
        Assert.Equal(Money.FromEuros(1000 - 250), books.Cash(0));
        Assert.True(books.Check(1, moneyCreation: false).IsSuccess);
    }

    // ---- rationing --------------------------------------------------------------------------

    /// <summary>
    /// First-come within a random order: when supply binds, the probability that a willing and
    /// able household is served does not depend on its income. The statistic is served ÷ (served +
    /// blocked) on the most oversubscribed shelf, by income tercile, over 25 ticks; the terciles
    /// agree to within four standard errors of a binomial share.
    /// </summary>
    [Fact]
    public void RationingIsIndependentOfIncome()
    {
        var simulation = new Simulation(Defaults, runSeed: 9);
        var events = Observe(simulation, ticks: 25);
        var population = simulation.Population;

        var shelf = events
            .Where(e => e.Outcome == WalkOutcome.Blocked)
            .GroupBy(e => (e.Category, e.Tier))
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        var attempts = events
            .Where(e => e.Category == shelf.Category && e.Tier == shelf.Tier && e.Outcome != WalkOutcome.Unaffordable)
            .ToArray();

        var ranked = Enumerable.Range(0, population.Count).OrderBy(h => population.Income[h].Cents).ToArray();
        var tercileOf = new int[population.Count];
        for (var i = 0; i < ranked.Length; i++)
        {
            tercileOf[ranked[i]] = i * 3 / ranked.Length;
        }

        var shares = new double[3];
        var counts = new int[3];

        for (var t = 0; t < 3; t++)
        {
            var inTercile = attempts.Where(a => tercileOf[a.Household] == t).ToArray();
            counts[t] = inTercile.Length;
            shares[t] = inTercile.Count(a => a.Outcome == WalkOutcome.Taken) / (double)inTercile.Length;
        }

        var pooled = attempts.Count(a => a.Outcome == WalkOutcome.Taken) / (double)attempts.Length;
        var smallest = counts.Min();
        var standardError = Math.Sqrt(pooled * (1 - pooled) / smallest);

        Assert.True(smallest > 500, $"only {smallest} attempts in a tercile");

        for (var t = 0; t < 3; t++)
        {
            Assert.True(
                Math.Abs(shares[t] - pooled) < 4 * standardError,
                $"shelf ({shelf.Category}, {shelf.Tier}): tercile {t} served {shares[t]:0.000} against {pooled:0.000} pooled, SE {standardError:0.000}");
        }
    }

    // ---- Engel, the purchase half -----------------------------------------------------------

    /// <summary>
    /// The other half of 04-01's Engel test. With the floor, every household on €300 or less is
    /// willing to buy food; with `a_g` forced to zero none of them is — food has become a luxury,
    /// and the poor end of the distribution stops eating without anything else in the run noticing.
    /// </summary>
    [Fact]
    public void WithoutTheFloor_PoorHouseholdsBuyNoFood()
    {
        var withFloor = new Simulation(Defaults, runSeed: 6);
        var noFloor = new Simulation(
            Defaults with { Categories = Defaults.Categories.Select((c, i) => i == Food ? c with { Necessity = 0.0 } : c).ToArray() },
            runSeed: 6);

        // Households on €300 or less for whom, with the floor, budget food clears λ. Taste varies,
        // so a household with w = 0.75 on €300 is not willing even with the floor; the claim is
        // about the floor, so the set is drawn where the floor is what makes the difference.
        var ladder = new Candidate[withFloor.Goods.TierCount];
        var poor = Enumerable.Range(0, withFloor.Population.Count)
            .Where(h => withFloor.Population.Income[h] <= Money.FromEuros(300))
            .Where(h =>
            {
                Ladder.Build(withFloor.Goods, withFloor.Market, withFloor.Population, h, Food, ladder);
                return ladder[Budget].Score >= Defaults.Decision.Lambda;
            })
            .ToHashSet();
        Assert.True(poor.Count > 10, $"{poor.Count} poor households");

        var fed = Observe(withFloor, ticks: 1).Where(e => e.Category == Food && poor.Contains(e.Household)).ToArray();
        var unfed = Observe(noFloor, ticks: 1).Where(e => e.Category == Food && poor.Contains(e.Household)).ToArray();

        Assert.Equal(poor.Count, fed.Select(e => e.Household).Distinct().Count());
        Assert.Empty(unfed);
    }

    // ---- allocation -------------------------------------------------------------------------

    /// <summary>Zero managed allocations for a full tick, under both rationing rules.</summary>
    [Theory]
    [InlineData(Rationing.Random)]
    [InlineData(Rationing.Willingness)]
    public void AFullTickAllocatesNothing(Rationing rationing)
    {
        var simulation = new Simulation(Defaults with { Prices = Defaults.Prices with { Rationing = rationing } }, runSeed: 12);
        var tick = 0;
        Result? measured = null;

        var allocated = Infrastructure.Allocations.Of(
            () => Assert.True(Results.IsOk(simulation.RunTick(++tick))),
            () => measured = simulation.RunTick(++tick));

        Assert.Equal(0, allocated);
        Assert.True(Results.IsOk(measured!));
    }

    // ---- the run --------------------------------------------------------------------------------

    /// <summary>
    /// V1 holds every tick with the walk running. Thirty ticks: without repricing (E5) the town
    /// spends well below its income at opening prices and the pool drains, so a full 360-tick run
    /// is E5's test, not this one.
    /// </summary>
    [Fact]
    public void V1HoldsEveryTickWithTheWalkRunning()
    {
        var simulation = new Simulation(Defaults, runSeed: 13);
        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= 30; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");
            Assert.Equal(simulation.Books.M0, simulation.Books.MoneyHeld);
        }
    }
}
