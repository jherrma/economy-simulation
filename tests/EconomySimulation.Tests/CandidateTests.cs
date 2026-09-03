using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/04-03. Candidates are upgrades, not tiers.</summary>
public sealed class CandidateTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static readonly GoodsTable Goods = new(Defaults);

    private static readonly Market Opening = new(Goods);

    private static readonly double Lambda = Defaults.Decision.Lambda;

    private const int Food = 0;
    private const int Leisure = 1;
    private const int Clothing = 2;
    private const int Hobby = 3;
    private const int Electronics = 4;
    private const int Appliances = 5;

    private static Households One(int euros, double taste = 1.0) =>
        Households.Specified(Goods.CategoryCount, [Money.FromEuros(euros)], [taste]);

    private static Candidate[] LadderFor(Households population, int category, Market? market = null)
    {
        var buffer = new Candidate[Goods.TierCount];
        var n = Ladder.Build(Goods, market ?? Opening, population, 0, category, buffer);

        Assert.Equal(Goods.TierCount, n);
        return buffer;
    }

    // ---- the three increments -------------------------------------------------------------

    /// <summary>`01-SIMULATION.md` §5.1, row for row, at the median: Δvalue and Δcost of each step.</summary>
    [Fact]
    public void TheThreeCandidatesAreTheSpecificationsIncrements()
    {
        var ladder = LadderFor(One(650), Food);
        var v = 403.00;
        var p = 300.00;

        Assert.Equal(v * 0.68, ladder[0].DeltaValue.EurosPerTick, precision: 9);
        Assert.Equal(v * 0.32, ladder[1].DeltaValue.EurosPerTick, precision: 9);
        Assert.Equal(v * 0.40, ladder[2].DeltaValue.EurosPerTick, precision: 9);

        Assert.Equal(p * 0.60, ladder[0].DeltaPrice.Cents / 100.0);
        Assert.Equal(p * 0.40, ladder[1].DeltaPrice.Cents / 100.0);
        Assert.Equal(p * 0.80, ladder[2].DeltaPrice.Cents / 100.0);

        Assert.False(ladder[0].IsUpgrade);
        Assert.True(ladder[1].IsUpgrade);
        Assert.True(ladder[2].IsUpgrade);
    }

    /// <summary>
    /// The increments sum exactly to the tier's posted price, to the cent, for every category and
    /// every tier. They are differences of posted prices, so they telescope: no split is needed
    /// and no residual cent can appear.
    /// </summary>
    [Fact]
    public void IncrementsSumExactlyToTheTierPrice()
    {
        var population = One(650);

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var ladder = LadderFor(population, c);
            var paid = Money.Zero;

            for (var t = 0; t < Goods.TierCount; t++)
            {
                paid += ladder[t].DeltaPrice;
                Assert.Equal(Opening.Price(c, t), paid);
            }

            // And the spec's own numbers: 0.60 + 0.40 = 1.00, + 0.80 = 1.80.
            var reference = Goods.Categories[c].PriceRef;
            Assert.Equal(reference, ladder[0].DeltaPrice + ladder[1].DeltaPrice);
            Assert.Equal(reference.Scaled(1.80), ladder[0].DeltaPrice + ladder[1].DeltaPrice + ladder[2].DeltaPrice);
        }
    }

    /// <summary>
    /// The uniform multipliers on the base score — 1.133, 0.800, 0.500 — derived from the tier
    /// parameters, not typed in. They are uniform across categories because both sides of every
    /// increment are proportional to the same base.
    /// </summary>
    [Fact]
    public void TheUpgradeMultipliersAreDerivedFromTheTierParameters()
    {
        var tiers = Goods.Tiers;
        var expected = new double[Goods.TierCount];

        for (var t = 0; t < Goods.TierCount; t++)
        {
            var dValue = tiers[t].ValueMult - (t > 0 ? tiers[t - 1].ValueMult : 0.0);
            var dPrice = tiers[t].PriceMult - (t > 0 ? tiers[t - 1].PriceMult : 0.0);
            expected[t] = dValue / dPrice;
        }

        Assert.Equal(1.133, expected[0], precision: 3);
        Assert.Equal(0.800, expected[1], precision: 3);
        Assert.Equal(0.500, expected[2], precision: 3);

        int[] incomes = [300, 650, 3000];

        foreach (var income in incomes)
        {
            var population = One(income);

            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                var life = Goods.Categories[c].Life;
                var baseScore = Valuation.Score(
                    Valuation.BaseValue(Goods, population, 0, c),
                    Valuation.FlowCost(Goods.Categories[c].PriceRef, life));

                var ladder = LadderFor(population, c);

                for (var t = 0; t < Goods.TierCount; t++)
                {
                    Assert.Equal(baseScore * expected[t], ladder[t].Score, precision: 9);
                }
            }
        }
    }

    // ---- monotonicity ---------------------------------------------------------------------

    /// <summary>
    /// Budget > budget→standard > standard→premium, for any reference price, any income and any
    /// taste, so long as tier prices stand in their opening ratios. A property test rather than an
    /// example: if a parameter change ever inverts it, the ladder inverts, everyone jumps to
    /// premium, and the output still looks like output. 02-02 rejects such a configuration at
    /// load; this catches it if that check is ever weakened.
    /// </summary>
    [Fact]
    public void UpgradeScoresAreMonotoneDecreasing_ForRandomisedPricesAndIncomes()
    {
        var random = RandomStream.ForTick(runSeed: 404, tick: 3, Purpose.Order);
        var buffer = new Candidate[Goods.TierCount];

        for (var trial = 0; trial < 2000; trial++)
        {
            var priceRef = Money.FromEuros(1 + random.NextInt(5000));
            var income = Money.FromEuros(100 + random.NextInt(10_000));
            var taste = 0.5 + (1.5 * random.NextDouble());
            var life = 1 + random.NextInt(120);
            var category = random.NextInt(Goods.CategoryCount);

            var parameters = Defaults with
            {
                Categories = Defaults.Categories
                    .Select((c, i) => i == category ? c with { PriceRef = priceRef, Life = life, Capacity = Math.Max(1, 1000 / life) } : c)
                    .ToArray(),
            };
            var goods = new GoodsTable(parameters);
            var market = new Market(goods);
            var population = Households.Specified(goods.CategoryCount, [income], [taste]);

            Ladder.Build(goods, market, population, 0, category, buffer);

            for (var t = 1; t < goods.TierCount; t++)
            {
                Assert.True(
                    buffer[t].Score < buffer[t - 1].Score,
                    $"trial {trial}: price_ref {priceRef}, income {income}, w {taste:0.00}, life {life}: "
                    + $"step {t} scores {buffer[t].Score:0.000} against {buffer[t - 1].Score:0.000} below it");
            }
        }
    }

    /// <summary>The same for every household of a drawn population, at opening prices.</summary>
    [Fact]
    public void UpgradeScoresAreMonotoneDecreasing_ForEveryDrawnHousehold()
    {
        var population = Households.Draw(Defaults, Goods, runSeed: 11);
        var buffer = new Candidate[Goods.TierCount];

        for (var h = 0; h < population.Count; h++)
        {
            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                Ladder.Build(Goods, Opening, population, h, c, buffer);

                Assert.True(buffer[0].Score > buffer[1].Score && buffer[1].Score > buffer[2].Score);
            }
        }
    }

    /// <summary>
    /// And the limit of the property: it depends on the tier price *ratios*, which repricing is
    /// allowed to move. Budget rising to more than 0.68 of standard makes the upgrade a better
    /// ratio than the purchase. Documented here so the walk's ranking is understood not to rely on
    /// the ladder arriving sorted.
    /// </summary>
    [Fact]
    public void AfterIndependentRepricing_TheLadderCanInvert()
    {
        var market = new Market(Goods);
        market.SetPrice(Food, 0, Money.FromEuros(210)); // 0.70 of standard's 300, up from 0.60

        var ladder = LadderFor(One(650), Food, market);

        Assert.True(ladder[1].Score > ladder[0].Score, $"{ladder[0].Score:0.000} vs {ladder[1].Score:0.000}");
    }

    // ---- the ladder table -----------------------------------------------------------------

    /// <summary>
    /// `02-PARAMETERS.md` §3.4, the median household by candidate: every score to three decimals,
    /// and the tier each category lands on.
    /// </summary>
    [Theory]
    [InlineData(Food, 1.343, 1.522, 1.075, 0.672, 1)]
    [InlineData(Leisure, 1.300, 1.473, 1.040, 0.650, 1)]
    [InlineData(Electronics, 1.248, 1.414, 0.998, 0.624, 0)]
    [InlineData(Appliances, 1.248, 1.414, 0.998, 0.624, 0)]
    [InlineData(Hobby, 1.144, 1.297, 0.915, 0.572, 0)]
    [InlineData(Clothing, 1.073, 1.216, 0.858, 0.536, 0)]
    public void TheMedianHouseholdsScores_MatchTheSpecificationRowForRow(
        int category, double baseScore, double budget, double toStandard, double toPremium, int chosen)
    {
        var population = One(650);
        var ladder = LadderFor(population, category);

        var life = Goods.Categories[category].Life;
        var actualBase = Valuation.Score(
            Valuation.BaseValue(Goods, population, 0, category),
            Valuation.FlowCost(Goods.Categories[category].PriceRef, life));

        // Three published decimals: the true value lies within half a unit of the last place,
        // plus a little for the specification's own rounding (clothing's base is exactly 1.0725).
        const double half = 0.0006;
        Assert.InRange(actualBase, baseScore - half, baseScore + half);
        Assert.InRange(ladder[0].Score, budget - half, budget + half);
        Assert.InRange(ladder[1].Score, toStandard - half, toStandard + half);
        Assert.InRange(ladder[2].Score, toPremium - half, toPremium + half);

        Assert.Equal(chosen, Ladder.UnconstrainedTier(ladder, Lambda));
    }

    /// <summary>
    /// The whole ladder table of §3.4: six incomes, six categories, and the per-tick spend each
    /// row implies. This is the decision rule with every constraint removed.
    /// </summary>
    [Theory]
    [InlineData(300, new[] { 0, -1, -1, -1, -1, -1 }, 180.00)]
    [InlineData(450, new[] { 0, 0, 0, -1, 0, 0 }, 360.00)]
    [InlineData(650, new[] { 1, 1, 0, 0, 0, 0 }, 590.00)]
    [InlineData(900, new[] { 1, 1, 1, 1, 1, 1 }, 650.00)]
    [InlineData(1500, new[] { 1, 2, 1, 2, 2, 2 }, 876.67)]
    [InlineData(3000, new[] { 2, 2, 2, 2, 2, 2 }, 1170.00)]
    public void TheLadderTable_IsReproducedForEveryIncome(int income, int[] tiers, double spendPerTick)
    {
        var population = One(income);
        var spend = 0.0;

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var ladder = LadderFor(population, c);
            var chosen = Ladder.UnconstrainedTier(ladder, Lambda);

            Assert.True(tiers[c] == chosen, $"€{income}, {Goods.Categories[c].Name}: expected tier {tiers[c]}, chose {chosen}");

            if (chosen >= 0)
            {
                spend += Opening.Price(c, chosen).Cents / 100.0 / Goods.Categories[c].Life;
            }
        }

        Assert.Equal(spendPerTick, spend, precision: 2);
    }

    /// <summary>
    /// The three design requirements §3.4 encodes: essentials outrank durables where the budget
    /// binds; the median sits mid-ladder; the premium step never clears at the median.
    ///
    /// The first was written as "at every income" and is not: food's value is mostly floor, so
    /// its score grows slowly with income while a durable's grows fast, and electronics overtakes
    /// food at about €770. The specification was corrected on finding this, not the parameters.
    /// This test pins both halves — the ordering holds at the median and below, and the crossing
    /// is where the arithmetic puts it — so a future parameter change that moves it is noticed.
    /// </summary>
    [Fact]
    public void TheThreeDesignRequirementsHold()
    {
        int[] protectedIncomes = [300, 450, 650, 750];

        foreach (var income in protectedIncomes)
        {
            var population = One(income);
            var foodBudget = LadderFor(population, Food)[0].Score;

            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                if (Goods.IsDurable(c))
                {
                    Assert.True(foodBudget > LadderFor(population, c)[0].Score, $"€{income}: a durable outranks food");
                }
            }
        }

        // The crossing: electronics is below food at €760 and above it at €775.
        Assert.True(LadderFor(One(760), Food)[0].Score > LadderFor(One(760), Electronics)[0].Score);
        Assert.True(LadderFor(One(775), Food)[0].Score < LadderFor(One(775), Electronics)[0].Score);

        var median = One(650);

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var ladder = LadderFor(median, c);

            Assert.True(ladder[0].Score >= Lambda, "the median buys every category");
            Assert.True(ladder[2].Score < Lambda, "the premium step clears at the median");

            if (Goods.IsDurable(c))
            {
                Assert.InRange(ladder[1].Score, 0.85, 1.0);
            }
        }
    }

    // ---- the wrong implementation ---------------------------------------------------------

    /// <summary>
    /// Ranking whole tiers by `value_mult / price_mult` puts budget first for every household at
    /// any income, so premium never sells. The increment rule sells premium to the household on
    /// €3000. This is the mechanism the tiers were added for, and it exists only under one of the
    /// two rules.
    /// </summary>
    [Fact]
    public void RankingWholeTiers_WouldNeverSellPremium()
    {
        var rich = One(3000);

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var bestTier = Enumerable.Range(0, Goods.TierCount)
                .OrderByDescending(t => Goods.Tiers[t].ValueMult / Goods.Tiers[t].PriceMult)
                .First();

            Assert.Equal(0, bestTier);
            Assert.Equal(2, Ladder.UnconstrainedTier(LadderFor(rich, c), Lambda));
        }
    }

    // ---- allocation -----------------------------------------------------------------------

    [Fact]
    public void BuildingEveryLadderForEveryHousehold_AllocatesNothing()
    {
        var population = Households.Draw(Defaults, Goods, runSeed: 2);
        Span<Candidate> buffer = stackalloc Candidate[Goods.GoodCount];

        Ladder.Build(Goods, Opening, population, 0, 0, buffer);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var taken = 0;
        var written = 0;

        // No assertions inside the loop: xunit's Assert.Equal allocates, and would be measured.
        for (var h = 0; h < population.Count; h++)
        {
            var n = 0;

            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                n += Ladder.Build(Goods, Opening, population, h, c, buffer[n..]);
            }

            written += n;
            taken += Ladder.UnconstrainedTier(buffer[..Goods.TierCount], Lambda) + 1;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(Goods.GoodCount * population.Count, written);
        Assert.True(taken > 0);
        Assert.Equal(0, allocated);
    }
}
