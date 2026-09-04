using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.Output;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/07-03. Four numbers about the households that never borrow.</summary>
public sealed class CohortMetricsTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private const int Food = 0;
    private const int Electronics = 4;
    private const int Budget = 0;
    private const int Premium = 2;

    private static Simulation Run(SimulationParameters parameters, int seed, int ticks)
    {
        var simulation = new Simulation(parameters, seed);
        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= ticks; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");
        }

        return simulation;
    }

    // ---- the cohorts themselves ---------------------------------------------------------------

    /// <summary>The two cohorts partition the population exactly: everyone is in one, nobody in both.</summary>
    [Fact]
    public void TheCohortsPartitionThePopulation()
    {
        var simulation = Run(Defaults, seed: 1, ticks: 1);
        var cohorts = simulation.Cohorts;

        var abstainers = 0;
        var borrowers = 0;

        for (var h = 0; h < simulation.Population.Count; h++)
        {
            var cohort = CohortMetrics.Of(simulation.Population, h);

            if (cohort == Cohort.Abstainer)
            {
                abstainers++;
                Assert.Equal(0.0, simulation.Population.Theta[h]);
            }
            else
            {
                borrowers++;
            }
        }

        Assert.Equal(simulation.Population.Count, abstainers + borrowers);
        Assert.Equal(abstainers, cohorts.Households(Cohort.Abstainer));
        Assert.Equal(borrowers, cohorts.Households(Cohort.Borrower));

        // Roughly the configured share; drawn per household, so it varies a little by seed.
        Assert.InRange(abstainers / (double)simulation.Population.Count, 0.15, 0.25);
    }

    /// <summary>
    /// Membership is identical across scenarios for a given seed. The finding is a difference
    /// between two runs for the *same* fifth of households; if the membership moved, the difference
    /// would contain a composition change nobody could decompose.
    /// </summary>
    [Fact]
    public void MembershipIsIdenticalAcrossScenariosOnOneSeed()
    {
        var off = Run(Defaults, seed: 4, ticks: 1);
        var high = Run(
            Defaults with { Credit = Defaults.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 } },
            seed: 4,
            ticks: 1);

        Assert.Equal(off.Population.IsAbstainer, high.Population.IsAbstainer);
        Assert.Equal(
            off.Cohorts.Households(Cohort.Abstainer),
            high.Cohorts.Households(Cohort.Abstainer));

        // And an abstainer has θ = 0 in the scenario that gives everyone else 0.4 to 0.9.
        for (var h = 0; h < high.Population.Count; h++)
        {
            if (high.Population.IsAbstainer[h])
            {
                Assert.Equal(0.0, high.Population.Theta[h]);
            }
            else
            {
                Assert.InRange(high.Population.Theta[h], 0.4, 0.9);
            }
        }
    }

    // ---- the four measures --------------------------------------------------------------------

    /// <summary>Units obtained never exceed units wanted, and both cohorts' totals reconcile with what the shelves sold.</summary>
    [Fact]
    public void ObtainedAndWantedReconcileWithTheMarket()
    {
        var simulation = Run(Defaults, seed: 2, ticks: 6);
        var cohorts = simulation.Cohorts;

        for (var c = 0; c < simulation.Goods.CategoryCount; c++)
        {
            var sold = 0;

            for (var t = 0; t < simulation.Goods.TierCount; t++)
            {
                sold += simulation.Market.Sold(c, t);

                Assert.Equal(
                    sold - (t > 0 ? sold - simulation.Market.Sold(c, t) : 0),
                    cohorts.Units(Cohort.Abstainer, c, t) + cohorts.Units(Cohort.Borrower, c, t));
            }

            var obtained = cohorts.Obtained(Cohort.Abstainer, c) + cohorts.Obtained(Cohort.Borrower, c);
            var wanted = cohorts.Wanted(Cohort.Abstainer, c) + cohorts.Wanted(Cohort.Borrower, c);

            Assert.Equal(sold, obtained);
            Assert.True(obtained <= wanted, $"category {c}: {obtained} obtained of {wanted} wanted");
        }
    }

    /// <summary>The cohorts' cash and outstanding principal add up to the ledger's, to the cent.</summary>
    [Fact]
    public void TheCohortBalancesAddUpToTheLedgers()
    {
        var high = Defaults with { Credit = Defaults.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 } };
        var simulation = Run(high, seed: 1, ticks: 18);
        var cohorts = simulation.Cohorts;

        var cash = cohorts.Cash(Cohort.Abstainer) + cohorts.Cash(Cohort.Borrower);
        var loans = cohorts.LoansOutstanding(Cohort.Abstainer) + cohorts.LoansOutstanding(Cohort.Borrower);

        Assert.Equal(simulation.Books.MoneyHeld - simulation.Books.Pool, cash);
        Assert.Equal(simulation.Books.LoansOutstanding, loans);

        // Abstainers never borrow, in any scenario, so their side of both credit columns is zero.
        Assert.Equal(Money.Zero, cohorts.LoansOutstanding(Cohort.Abstainer));
        Assert.Equal(Money.Zero, cohorts.DebtService(Cohort.Abstainer));
        Assert.True(cohorts.LoansOutstanding(Cohort.Borrower) > Money.Zero);
    }

    /// <summary>Spend is the posted price of the tier each household landed on, summed — not the increments, and not the ladder.</summary>
    [Fact]
    public void SpendIsThePostedPriceOfTheTierLandedOn()
    {
        var simulation = Run(Defaults, seed: 3, ticks: 4);
        var cohorts = simulation.Cohorts;

        for (var c = 0; c < simulation.Goods.CategoryCount; c++)
        {
            var expected = Money.Zero;

            for (var t = 0; t < simulation.Goods.TierCount; t++)
            {
                var atTier = cohorts.Units(Cohort.Abstainer, c, t) + cohorts.Units(Cohort.Borrower, c, t);
                expected += simulation.Recorded.PriceTraded(c, t) * atTier;
            }

            Assert.Equal(expected, cohorts.Spend(Cohort.Abstainer, c) + cohorts.Spend(Cohort.Borrower, c));
        }
    }

    /// <summary>
    /// The measure the model was rebuilt to produce. A cohort that obtains exactly as many units as
    /// before, but at a lower tier, has an unchanged unit count and a **fallen** quality index —
    /// which is the trade-down finding in one number.
    /// </summary>
    [Fact]
    public void ACohortThatKeepsItsUnitsButBuysWorse_ShowsAFallenQualityIndex()
    {
        var goods = new GoodsTable(Defaults);
        var population = Households.Specified(goods, [Money.FromEuros(650)], [1.0]);
        var metrics = new CohortMetrics(goods, population, ticks: 10);

        metrics.OpenTick();
        metrics.RecordWant(Cohort.Abstainer, Food);
        metrics.RecordPurchase(Cohort.Abstainer, Food, Premium, Money.FromEuros(540), wait: 0);

        var premium = metrics.Quality(Cohort.Abstainer);
        var units = metrics.Obtained(Cohort.Abstainer);

        metrics.OpenTick();
        metrics.RecordWant(Cohort.Abstainer, Food);
        metrics.RecordPurchase(Cohort.Abstainer, Food, Budget, Money.FromEuros(180), wait: 0);

        Assert.Equal(units, metrics.Obtained(Cohort.Abstainer));
        Assert.True(metrics.Quality(Cohort.Abstainer) < premium);
        Assert.Equal(goods.Tiers[Budget].ValueMult, metrics.Quality(Cohort.Abstainer));
        Assert.Equal(goods.Tiers[Premium].ValueMult, premium);
    }

    // ---- the wait -----------------------------------------------------------------------------

    /// <summary>
    /// The wait counts unmet wants at their current age rather than dropping them. Two households
    /// want electronics: one is served this tick at wait 0, the other has been waiting three ticks
    /// and is not served. The median over both is 1.5, not 0 — dropping the unserved one would make
    /// a cohort that never gets served look patient, and under `credit_high` those are exactly the
    /// abstainers the finding is about.
    /// </summary>
    [Fact]
    public void UnmetWantsAreCountedAtTheirCurrentAge()
    {
        var goods = new GoodsTable(Defaults);
        var population = Households.Specified(goods, [Money.FromEuros(650), Money.FromEuros(650)], [1.0, 1.0]);
        Array.Fill(population.IsAbstainer, true);

        var books = Ledger.Open([Money.Zero, Money.Zero], Money.Zero);
        var loans = new LoanBook(population, 1);
        var metrics = new CohortMetrics(goods, population, ticks: 10);

        metrics.OpenTick();

        // One served now; one still wanting, three ticks in.
        metrics.RecordPurchase(Cohort.Abstainer, Electronics, Budget, Money.FromEuros(540), wait: 0);

        var open_ = population.AgeIndex(1, Electronics);
        population.Wanted[open_] = true;
        population.Wait[open_] = 3;

        metrics.Close(population, books, loans);

        Assert.Equal(1.5, metrics.WaitMedian(Cohort.Abstainer));
    }

    /// <summary>Food and leisure are met or not within the tick, so they are left out of the wait: their zeros would swamp it.</summary>
    [Fact]
    public void TheWaitIsAboutDurablesOnly()
    {
        var goods = new GoodsTable(Defaults);
        var population = Households.Specified(goods, [Money.FromEuros(650)], [1.0]);
        Array.Fill(population.IsAbstainer, true);

        var books = Ledger.Open([Money.Zero], Money.Zero);
        var loans = new LoanBook(population, 1);
        var metrics = new CohortMetrics(goods, population, ticks: 10);

        metrics.OpenTick();

        // A hundred food purchases at wait 0, and one durable want four ticks old.
        for (var n = 0; n < 100; n++)
        {
            metrics.RecordPurchase(Cohort.Abstainer, Food, Budget, Money.FromEuros(180), wait: 0);
        }

        var open_ = population.AgeIndex(0, Electronics);
        population.Wanted[open_] = true;
        population.Wait[open_] = 4;

        metrics.Close(population, books, loans);

        Assert.Equal(4.0, metrics.WaitMedian(Cohort.Abstainer));
    }

    /// <summary>
    /// The rationing is an exclusion, not a queue (`01-SIMULATION.md` §10.1). A household that gets
    /// served is served the tick it asks — the median wait among wants actually met is zero — while
    /// the median over every open want sits far above it, held up by a stable population at the
    /// poor end that is never served at all.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheRationingIsAnExclusionRatherThanAQueue(int seed)
    {
        var simulation = Run(Defaults, seed, ticks: 200);
        var cohorts = simulation.Cohorts;

        foreach (var cohort in new[] { Cohort.Abstainer, Cohort.Borrower })
        {
            Assert.Equal(0.0, cohorts.WaitMedianMet(cohort));
            Assert.True(cohorts.WaitMedian(cohort) > 5.0, $"{cohort}: open median {cohorts.WaitMedian(cohort)}");
        }
    }

    /// <summary>
    /// And why a gate may not ask it to be flat: over a stationary stretch of the baseline the open
    /// median swings several-fold from tick to tick, because it is a flow of fresh wants measured
    /// against a growing stock of stuck ones. The met median does not move at all.
    /// </summary>
    [Fact]
    public void TheOpenMedianIsNotAStationarySeries()
    {
        var simulation = new Simulation(Defaults, runSeed: 1);
        Assert.True(simulation.Start().IsSuccess);

        var open = new List<double>();
        var met = new List<double>();

        for (var tick = 1; tick <= 180; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);

            if (tick > 120)
            {
                open.Add(simulation.Cohorts.WaitMedian(Cohort.Abstainer));
                met.Add(simulation.Cohorts.WaitMedianMet(Cohort.Abstainer));
            }
        }

        Assert.True(open.Max() > open.Min() * 2, $"the open median ranged {open.Min()} to {open.Max()} over sixty stationary ticks");
        Assert.All(met, m => Assert.Equal(0.0, m));
    }

    // ---- the file -----------------------------------------------------------------------------

    /// <summary>Every cohort series reaches run.csv, under names a reader can key on.</summary>
    [Fact]
    public void TheCohortSeriesAreWrittenToRunCsv()
    {
        var directory = Path.Combine(Path.GetTempPath(), "economy-sim-" + Guid.NewGuid().ToString("N"));

        try
        {
            var simulation = new Simulation(Defaults, runSeed: 1);
            Assert.True(simulation.Start().IsSuccess);

            var created = MetricsWriter.Create(simulation, directory);
            Assert.True(created.IsSuccess);

            using (var writer = created.Value)
            {
                for (var tick = 1; tick <= 3; tick++)
                {
                    Assert.True(simulation.RunTick(tick).IsSuccess);
                    Assert.True(writer.Write(simulation).IsSuccess);
                }

                Assert.True(writer.Finish().IsSuccess);
            }

            var run = Csv.Read(Path.Combine(directory, "run.csv"));

            foreach (var cohort in new[] { "abstainer", "borrower" })
            {
                foreach (var series in new[] { "households", "cash", "loans_outstanding", "debt_service", "spend", "quality", "wanted", "obtained", "wait_median", "wait_median_met" })
                {
                    Assert.True(run.Has($"{cohort}_{series}"), $"run.csv has no column '{cohort}_{series}'");
                }

                Assert.True(run.Has($"{cohort}_electronics_wanted"));
                Assert.True(run.Has($"{cohort}_electronics_obtained"));
                Assert.True(run.Has($"{cohort}_electronics_spend"));
                Assert.True(run.Has($"{cohort}_electronics_premium_units"));
            }

            var last = run.RowCount - 1;
            Assert.Equal(
                simulation.Population.Count,
                run.Integer(last, "abstainer_households") + run.Integer(last, "borrower_households"));
            Assert.Equal("0.00", run.Text(last, "abstainer_loans_outstanding"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
