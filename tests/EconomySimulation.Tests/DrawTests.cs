using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/02-04.</summary>
public sealed class DrawTests
{
    private const int Seed = 4242;

    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static Households Population(SimulationParameters parameters, int seed = Seed) =>
        Households.Draw(parameters, new GoodsTable(parameters), seed);

    private static SimulationParameters With(int households) =>
        Defaults with { Run = Defaults.Run with { Households = households } };

    // ---- income ------------------------------------------------------------------------------

    /// <summary>
    /// The sample mean is the mean income, not something near it.
    ///
    /// The `− σ²/2` term is what makes that true. The next test says what it is worth.
    /// </summary>
    [Fact]
    public void IncomeConvergesToTheMeanIncome()
    {
        var population = Population(With(100_000));

        var mean = population.Income.Sum(i => i.Cents) / (double)population.Count / 100.0;

        Assert.Equal(650.00, mean, 0.5 / 100.0 * 650.0);
    }

    /// <summary>
    /// Without the correction the mean would be `mean · exp(σ²/2)` — 6.3 per cent high at
    /// σ = 0.35. That is not a rounding error: it is the town earning six per cent more than the
    /// goods table supplies, and the symptom is a draining pool and what looks like inflation.
    ///
    /// The gap between the two is fifteen times the sampling error at this population size, so
    /// the test above genuinely distinguishes them.
    /// </summary>
    [Fact]
    public void WithoutTheCorrection_TheMeanWouldBeSixPerCentHigh()
    {
        const double sigma = 0.35;
        var uncorrected = Math.Exp(sigma * sigma / 2.0);

        Assert.Equal(1.0632, uncorrected, 4);

        var population = Population(With(100_000));
        var mean = population.Income.Sum(i => i.Cents) / (double)population.Count / 65000.0;

        Assert.True(
            Math.Abs(mean - 1.0) < Math.Abs(mean - uncorrected),
            $"The sample mean {mean:F4} is not closer to 1 than to the uncorrected {uncorrected:F4}.");
    }

    [Fact]
    public void IncomeIsSpreadWidelyEnoughToPopulateTheLadder()
    {
        var incomes = Population(With(10_000)).Income.Select(i => i.Cents / 100.0).Order().ToArray();

        // The ladder in §3.4 is read at €300 through €3,000; the draw has to reach both ends.
        Assert.True(incomes[0] < 300, $"The poorest household earns {incomes[0]:F2}, so the bottom of the ladder is empty.");
        Assert.True(incomes[^1] > 1500, $"The richest household earns {incomes[^1]:F2}, so the top of the ladder is empty.");
    }

    /// <summary>
    /// Opening cash is a month of income. It is computed here and held by the ledger — a balance
    /// has exactly one home, and 03-01 says which.
    /// </summary>
    [Fact]
    public void OpeningCashIsOneMonthOfIncome()
    {
        var population = Population(Defaults);
        var cash = population.OpeningCash(Defaults.Income.OpeningCashShare);

        for (var h = 0; h < population.Count; h++)
        {
            Assert.Equal(population.Income[h], cash[h]);
        }
    }

    // ---- taste --------------------------------------------------------------------------------

    /// <summary>
    /// Mean exactly 1 — same `− σ²/2` correction, same reason.
    ///
    /// The tolerance is stated against what it has to distinguish rather than picked for looking
    /// tight. At σ_w = 0.20 over 100,000 draws the standard error of the mean is about 0.00064,
    /// while dropping the correction would put the mean at exp(σ²/2) = 1.0202. Half a per cent
    /// sits eight standard errors from one and a quarter of the way to the alternative, so a pass
    /// means the correction is there and a fail is not sampling noise.
    /// </summary>
    [Fact]
    public void TheTasteWeightHasMeanOne()
    {
        var population = Population(With(100_000));
        var mean = population.TasteWeight.Average();

        Assert.Equal(1.0, mean, 0.005);
        Assert.True(
            Math.Abs(mean - 1.0) < Math.Abs(mean - Math.Exp(0.20 * 0.20 / 2.0)),
            $"The mean taste weight {mean:F4} is not closer to 1 than to the uncorrected 1.0202.");
    }

    // ---- abstainers ---------------------------------------------------------------------------

    /// <summary>
    /// The same households abstain in every scenario for a given seed.
    ///
    /// The finding is a difference between two runs for the same fifth of the town. If the
    /// membership moved between the arms, that difference would include a composition change and
    /// nobody could take it apart again.
    /// </summary>
    [Fact]
    public void TheAbstainerSetIsIdenticalAcrossScenarios()
    {
        var baseline = Population(Defaults);

        var creditHigh = Population(
            Defaults with
            {
                Credit = Defaults.Credit with
                {
                    CreditEnabled = true,
                    ThetaMin = 0.4,
                    ThetaMax = 0.9,
                },
            });

        Assert.Equal(baseline.IsAbstainer, creditHigh.IsAbstainer);
    }

    [Fact]
    public void AboutAFifthOfHouseholdsAbstain()
    {
        var population = Population(With(100_000));

        var share = population.IsAbstainer.Count(a => a) / (double)population.Count;

        Assert.Equal(0.20, share, 0.01);
    }

    // ---- theta ---------------------------------------------------------------------------------

    [Fact]
    public void AbstainersHaveThetaZero_InEveryScenario()
    {
        foreach (var credit in new[]
                 {
                     Defaults.Credit,
                     Defaults.Credit with { CreditEnabled = true },
                     Defaults.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 },
                 })
        {
            var population = Population(Defaults with { Credit = credit });

            for (var h = 0; h < population.Count; h++)
            {
                if (population.IsAbstainer[h])
                {
                    Assert.Equal(0.0, population.Theta[h]);
                }
            }
        }
    }

    [Fact]
    public void ThetaLiesInTheConfiguredRange()
    {
        var population = Population(
            Defaults with { Credit = Defaults.Credit with { ThetaMin = 0.4, ThetaMax = 0.9 } });

        for (var h = 0; h < population.Count; h++)
        {
            if (!population.IsAbstainer[h])
            {
                Assert.InRange(population.Theta[h], 0.4, 0.9);
            }
        }
    }

    /// <summary>
    /// The concrete form of the seam in 01-05: turning credit on changes θ and nothing else. Every
    /// other attribute of every household is identical, because each comes from its own stream.
    /// </summary>
    [Fact]
    public void TurningCreditOn_ChangesNothingButTheta()
    {
        var off = Population(Defaults);
        var on = Population(Defaults with { Credit = Defaults.Credit with { CreditEnabled = true } });

        Assert.Equal(off.Income, on.Income);
        Assert.Equal(off.TasteWeight, on.TasteWeight);
        Assert.Equal(off.IsAbstainer, on.IsAbstainer);
        Assert.Equal(off.Age, on.Age);

        // And θ itself is unchanged too, because the range is what a scenario moves, not the flag.
        Assert.Equal(off.Theta, on.Theta);
    }

    /// <summary>
    /// Widening the θ range moves θ and leaves everything else exactly where it was — including
    /// the ages, which are drawn after θ in the same loop.
    /// </summary>
    [Fact]
    public void WideningTheThetaRange_MovesNothingElse()
    {
        var low = Population(Defaults);
        var high = Population(
            Defaults with { Credit = Defaults.Credit with { ThetaMin = 0.4, ThetaMax = 0.9 } });

        Assert.Equal(low.Income, high.Income);
        Assert.Equal(low.TasteWeight, high.TasteWeight);
        Assert.Equal(low.IsAbstainer, high.IsAbstainer);
        Assert.Equal(low.Age, high.Age);
        Assert.NotEqual(low.Theta, high.Theta);
    }

    // ---- durable ages ---------------------------------------------------------------------------

    /// <summary>
    /// Replacement demand is flat, not clustered.
    ///
    /// Initialise every durable at age zero and the whole town replaces its appliances in the same
    /// month, forever. The output is a clean sawtooth with period `life`, it looks exactly like a
    /// business cycle, and there is nothing in this model that should produce one.
    /// </summary>
    [Fact]
    public void DurableAgesAreSpreadOverTheirLife()
    {
        var parameters = With(60_000);
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters, goods, Seed);

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            var life = goods.Categories[c].Life;
            var histogram = new int[life];

            if (life == 1)
            {
                continue; // no age to spread; ANonDurableIsAlwaysDueImmediately covers it
            }

            for (var h = 0; h < population.Count; h++)
            {
                var age = population.Age[population.AgeIndex(h, c)];

                // {1 … life}: an age of `life` is due in tick 1, an age of 1 in tick `life`.
                Assert.InRange(age, 1, life);
                histogram[age - 1]++;
            }

            var expected = population.Count / (double)life;

            foreach (var bucket in histogram)
            {
                Assert.True(
                    Math.Abs(bucket - expected) < 0.15 * expected,
                    $"Category {goods.Categories[c].Name}: a cohort of {bucket} against {expected:F0} "
                    + "expected, so replacement demand is clustered rather than flat.");
            }
        }
    }

    /// <summary>
    /// The replacement schedule that follows: over the first `life` ticks, the number of
    /// households due a replacement each tick is roughly constant. This is the property the ages
    /// exist for, asserted directly rather than inferred from the histogram.
    /// </summary>
    [Fact]
    public void ReplacementDemandIsFlatOverTheFirstLifeOfTheRun()
    {
        var parameters = With(60_000);
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters, goods, Seed);

        const int appliances = 5;
        var life = goods.Categories[appliances].Life;
        var dueAtTick = new int[life + 1];

        for (var h = 0; h < population.Count; h++)
        {
            // Wants are asked before ageing, so a household with age a is due when a + (t − 1) ≥ life:
            // at tick life − a + 1. Ages run {1 … life}, so every tick from 1 to life gets a cohort.
            dueAtTick[life - population.Age[population.AgeIndex(h, appliances)] + 1]++;
        }

        var expected = population.Count / (double)life;

        for (var tick = 1; tick <= life; tick++)
        {
            Assert.True(
                Math.Abs(dueAtTick[tick] - expected) < 0.20 * expected,
                $"Tick {tick} has {dueAtTick[tick]} replacements against {expected:F0} expected.");
        }
    }

    [Fact]
    public void ANonDurableIsAlwaysDueImmediately()
    {
        var population = Population(Defaults);

        // Food and leisure have life 1, so the only age they can hold is zero.
        for (var h = 0; h < population.Count; h++)
        {
            Assert.Equal(0, population.Age[population.AgeIndex(h, 0)]);
            Assert.Equal(0, population.Age[population.AgeIndex(h, 1)]);
        }
    }

    // ---- reproducibility and shape ------------------------------------------------------------------

    [Fact]
    public void TheSameSeedDrawsTheSamePopulation()
    {
        var first = Population(Defaults);
        var again = Population(Defaults);

        Assert.Equal(first.Income, again.Income);
        Assert.Equal(first.Theta, again.Theta);
        Assert.Equal(first.Age, again.Age);
    }

    [Fact]
    public void ADifferentSeedDrawsADifferentPopulation()
    {
        Assert.NotEqual(Population(Defaults).Income, Population(Defaults, Seed + 1).Income);
    }

    /// <summary>
    /// Adding a household disturbs nobody else's draw, because every stream is keyed by household
    /// id. This is what lets the population be scaled in a scenario without changing the town.
    /// </summary>
    [Fact]
    public void AddingHouseholds_LeavesTheExistingOnesWhereTheyWere()
    {
        var small = Population(With(1000));
        var large = Population(With(2000));

        for (var h = 0; h < small.Count; h++)
        {
            Assert.Equal(small.Income[h], large.Income[h]);
            Assert.Equal(small.Theta[h], large.Theta[h]);
            Assert.Equal(small.IsAbstainer[h], large.IsAbstainer[h]);
        }
    }

    /// <summary>
    /// Parallel arrays, not objects. A per-household object would be an allocation per household
    /// in the walk and, more importantly, a place for a mechanism to be added that then has to be
    /// threaded through every scenario.
    /// </summary>
    [Fact]
    public void TheEngineHasNoHouseholdObject()
    {
        var offenders = Repo
            .EngineSources()
            .Where(p => File.ReadAllText(p) is var text
                        && (text.Contains("class Household ", StringComparison.Ordinal)
                            || text.Contains("record Household ", StringComparison.Ordinal)
                            || text.Contains("struct Household ", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(offenders.Length == 0, "A per-household type exists in: " + string.Join(", ", offenders));
    }
}
