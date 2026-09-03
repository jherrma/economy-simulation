using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/04-04. One unit per category, and the replacement cycle as a consequence of `life`.</summary>
public sealed class WantsTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static readonly GoodsTable Goods = new(Defaults);

    private const int Food = 0;
    private const int Clothing = 2;
    private const int Appliances = 5;

    private static Households One() =>
        Households.Specified(Goods.CategoryCount, [Money.FromEuros(650)], [1.0]);

    // ---- the rule -------------------------------------------------------------------------

    [Fact]
    public void ANonDurableIsWantedEveryTick()
    {
        var population = One();

        for (var tick = 0; tick < 5; tick++)
        {
            population.RefreshWant(0, Food, life: 1);
            Assert.True(population.Wanted[population.AgeIndex(0, Food)]);
            population.Acquire(0, Food);
        }
    }

    /// <summary>A durable is wanted exactly when `age ≥ life`, and not a tick before.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(40, true)]
    public void ADurableIsWantedWhenItsAgeReachesItsLife(int age, bool wanted)
    {
        var population = One();
        var life = Goods.Categories[Clothing].Life;
        Assert.Equal(6, life);

        population.Age[population.AgeIndex(0, Clothing)] = age;
        population.RefreshWant(0, Clothing, life);

        Assert.Equal(wanted, population.Wanted[population.AgeIndex(0, Clothing)]);
    }

    /// <summary>
    /// At most one unit of a category per household per tick — a want is a bool, and there is
    /// nowhere in the state for a second unit to go. This is a real limitation of the model and it
    /// belongs in the write-up: it cannot represent buying more, only buying better.
    /// </summary>
    [Fact]
    public void AWantIsABool_NotACount()
    {
        Assert.IsType<bool[]>(One().Wanted);
    }

    // ---- persistence and wait -------------------------------------------------------------

    /// <summary>
    /// A household that wants a durable and does not obtain it keeps wanting it, tick after tick,
    /// with `wait` counting. It does not skip a cycle.
    /// </summary>
    [Fact]
    public void AnUnmetWantPersists_AndWaitCounts()
    {
        var population = One();
        var life = Goods.Categories[Clothing].Life;
        var i = population.AgeIndex(0, Clothing);
        population.Age[i] = life;

        for (var tick = 0; tick < 4; tick++)
        {
            population.RefreshWant(0, Clothing, life);

            Assert.True(population.Wanted[i], $"tick {tick}: want lapsed");
            Assert.Equal(tick, population.Wait[i]);

            population.Age[i]++; // ageing, with no purchase in between
        }
    }

    [Fact]
    public void APurchaseResetsAgeWantAndWait()
    {
        var population = One();
        var life = Goods.Categories[Clothing].Life;
        var i = population.AgeIndex(0, Clothing);
        population.Age[i] = life + 3;
        population.RefreshWant(0, Clothing, life);
        population.RefreshWant(0, Clothing, life);
        Assert.Equal(1, population.Wait[i]);

        population.Acquire(0, Clothing);

        Assert.Equal(0, population.Age[i]);
        Assert.False(population.Wanted[i]);
        Assert.Equal(0, population.Wait[i]);

        // And after ageing, it is not wanted again for a full life.
        for (var t = 1; t < life; t++)
        {
            population.Age[i]++;
            population.RefreshWant(0, Clothing, life);
            Assert.False(population.Wanted[i], $"wanted again at age {t}");
        }
    }

    /// <summary>
    /// A non-durable's wait also counts: a household priced out of food for three ticks running
    /// has waited three ticks, and buying resets it.
    /// </summary>
    [Fact]
    public void ANonDurablesWaitCountsTicksWithoutAPurchase()
    {
        var population = One();
        var i = population.AgeIndex(0, Food);

        population.RefreshWant(0, Food, 1);
        population.RefreshWant(0, Food, 1);
        population.RefreshWant(0, Food, 1);
        Assert.Equal(2, population.Wait[i]);

        population.Acquire(0, Food);
        population.RefreshWant(0, Food, 1);
        Assert.Equal(0, population.Wait[i]);
    }

    // ---- in the tick ----------------------------------------------------------------------

    /// <summary>
    /// Ageing runs after the walk: a unit acquired in tick t is at age 0 when ageing runs, age 1
    /// when the next tick asks, and so not wanted. Acquisition is simulated where the walk will
    /// sit — just before the ageing step — since there is no walk yet.
    /// </summary>
    [Fact]
    public void AUnitBoughtThisTick_IsNotWantedNextTick()
    {
        var simulation = new Simulation(Defaults, runSeed: 5);
        var population = simulation.Population;
        var everyWanted = new List<(int Household, int Category)>();

        simulation.StepObserver = step =>
        {
            if (step != TickStep.Ageing)
            {
                return;
            }

            everyWanted.Clear();

            for (var h = 0; h < population.Count; h++)
            {
                for (var c = 0; c < Goods.CategoryCount; c++)
                {
                    if (population.Wanted[population.AgeIndex(h, c)])
                    {
                        everyWanted.Add((h, c));
                        population.Acquire(h, c);
                    }
                }
            }
        };

        Assert.True(simulation.RunTick(1).IsSuccess);
        var boughtDurables = everyWanted.Where(w => Goods.IsDurable(w.Category)).ToArray();
        Assert.NotEmpty(boughtDurables);

        // Tick 2: after step 3 and before the (simulated) walk, none of them is wanted again, and
        // each is at age 1 — aged once, after the purchase, not before it.
        var checkedInTickTwo = false;
        var inner = simulation.StepObserver;
        simulation.StepObserver = step =>
        {
            if (step == TickStep.Walk)
            {
                foreach (var (h, c) in boughtDurables)
                {
                    Assert.Equal(1, population.Age[population.AgeIndex(h, c)]);
                    Assert.False(population.Wanted[population.AgeIndex(h, c)]);
                }

                checkedInTickTwo = true;
            }

            inner(step);
        };

        Assert.True(simulation.RunTick(2).IsSuccess);
        Assert.True(checkedInTickTwo);
    }

    /// <summary>
    /// 360 ticks, no credit, every want met: the aggregate replacement demand per category is flat
    /// — its mean is `households / life` and no tick is far from it. This is 02-04's uniform
    /// initial ages seen from the other end; with ages initialised at zero the same series is a
    /// sawtooth, and the next test shows it.
    /// </summary>
    [Fact]
    public void ReplacementDemandIsFlat_AndConvergesToHouseholdsOverLife()
    {
        var series = WantsPerTick(new Simulation(Defaults, runSeed: 21), ticks: 360);
        var n = Defaults.Run.Households;

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var life = Goods.Categories[c].Life;
            var expected = (double)n / life;
            var mean = series[c].Average();

            if (life == 1)
            {
                Assert.All(series[c], w => Assert.Equal(n, w));
                continue;
            }

            // Each tick's count is Binomial(n, 1/life); the mean over T ticks has standard error
            // sqrt(n · p · (1 − p) / T). Four of those, and a ceiling on any single tick.
            var p = 1.0 / life;
            var standardError = Math.Sqrt(n * p * (1 - p) / series[c].Length);
            var perTickSd = Math.Sqrt(n * p * (1 - p));

            Assert.InRange(mean, expected - (4 * standardError), expected + (4 * standardError));
            Assert.True(series[c].Max() < expected + (5 * perTickSd), $"{Goods.Categories[c].Name}: a tick with {series[c].Max()} wants against a mean of {expected:0.0}");
            Assert.True(series[c].Min() > Math.Max(0, expected - (5 * perTickSd)) - 1, $"{Goods.Categories[c].Name}: a tick with {series[c].Min()} wants");
        }
    }

    /// <summary>
    /// The control: a population whose durables all start at age 0 replaces everything in the
    /// same tick, once per life. For appliances that is one tick of 1000 wants and 95 ticks of
    /// none — a business cycle that nothing in this model should produce.
    /// </summary>
    [Fact]
    public void ZeroInitialisedAges_ProduceASawtooth()
    {
        var incomes = Enumerable.Repeat(Money.FromEuros(650), Defaults.Run.Households).ToArray();
        var tastes = Enumerable.Repeat(1.0, Defaults.Run.Households).ToArray();
        var population = Households.Specified(Goods.CategoryCount, incomes, tastes);
        var life = Goods.Categories[Appliances].Life;

        // Ages start at 0, so the spikes fall at ticks life and 2·life: one past the second is enough.
        var series = new int[(life * 2) + 1];

        for (var tick = 0; tick < series.Length; tick++)
        {
            for (var h = 0; h < population.Count; h++)
            {
                population.RefreshWant(h, Appliances, life);

                if (population.Wanted[population.AgeIndex(h, Appliances)])
                {
                    series[tick]++;
                    population.Acquire(h, Appliances);
                }
            }

            population.AgeDurables(Goods);
        }

        Assert.Equal(2, series.Count(w => w == population.Count));
        Assert.Equal(series.Length - 2, series.Count(w => w == 0));
    }

    // ---- helpers --------------------------------------------------------------------------

    /// <summary>Wants per category per tick, with every want met where the walk will sit.</summary>
    private static int[][] WantsPerTick(Simulation simulation, int ticks)
    {
        var population = simulation.Population;
        var series = Enumerable.Range(0, Goods.CategoryCount).Select(_ => new int[ticks]).ToArray();
        var tick = 0;

        simulation.StepObserver = step =>
        {
            if (step != TickStep.Ageing)
            {
                return;
            }

            for (var h = 0; h < population.Count; h++)
            {
                for (var c = 0; c < Goods.CategoryCount; c++)
                {
                    if (population.Wanted[population.AgeIndex(h, c)])
                    {
                        series[c][tick]++;
                        population.Acquire(h, c);
                    }
                }
            }
        };

        for (tick = 0; tick < ticks; tick++)
        {
            Assert.True(simulation.RunTick(tick + 1).IsSuccess);
        }

        return series;
    }
}
