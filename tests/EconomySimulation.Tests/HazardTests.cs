using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;
using static System.FormattableString;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/11-03. A durable fails with probability `1 / life` each tick, rather than at a
/// fixed age.
///
/// The rule it replaces cannot represent §3.7's realised lives at all — `Life` is an `int`, `Age` an
/// `int[]`, and 20.3 months is not a number a want can fire at. What made the hazard the answer
/// rather than a workaround is that three other things fall out of it: the initialisation sawtooth
/// cannot happen, because a geometric distribution has no age to initialise; the life-1 special case
/// disappears, because `p = 1` at `life = 1`; and `flow_cost = price / life` becomes literally the
/// expected cost per tick rather than an amortisation of a cycle the household is assumed to
/// complete.
/// </summary>
public sealed class HazardTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static SimulationParameters Hazard(
        bool credit = false,
        int households = 400,
        int ticks = 40,
        SimulationParameters? basis = null)
    {
        var b = basis ?? Defaults;

        return b with
        {
            Run = b.Run with
            {
                Replacement = Replacement.Hazard,
                Households = households,
                Ticks = ticks,
                WarmupTicks = 0,
                Scenario = credit ? "credit_high" : "credit_off",
            },
            Categories =
            [
                .. b.Categories.Select(c => c with { Capacity = (int)Math.Round(households / (double)c.Life, MidpointRounding.AwayFromZero) }),
            ],
            Credit = b.Credit with { CreditEnabled = credit, ThetaMin = credit ? 0.15 : 0.0, ThetaMax = credit ? 0.30 : 0.2 },
        };
    }

    // ---- the switch --------------------------------------------------------------------------

    /// <summary>
    /// `deterministic` is the default and nothing about a default run moves. The byte-for-byte half
    /// of that claim is V5's, on four seeds of real output; this is the schema half.
    /// </summary>
    [Fact]
    public void DeterministicIsTheDefault()
    {
        Assert.Equal(Replacement.Deterministic, Defaults.Run.Replacement);
        Assert.Contains("replacement = \"deterministic\"", Defaults.ToToml(), StringComparison.Ordinal);

        var loaded = ConfigurationLoader.FromToml("[run]\nreplacement = \"hazard\"");

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));
        Assert.Equal(Replacement.Hazard, loaded.Value.Run.Replacement);

        var wrong = ConfigurationLoader.FromToml("[run]\nreplacement = \"weibull\"");

        Assert.True(wrong.IsFailed);
        Assert.Contains(wrong.Errors, e => e.Message.Contains("deterministic", StringComparison.Ordinal));
    }

    /// <summary>Under `hazard` no age is drawn, and every household opens owning a working unit of everything.</summary>
    [Fact]
    public void EveryHouseholdOpensOwningAWorkingUnitOfEveryDurable()
    {
        var parameters = Hazard();
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters, goods, runSeed: 1);

        Assert.All(population.Holds, held => Assert.True(held));

        // And the age state is not merely unused, it is never set: `"initial_age"` is not consumed.
        Assert.All(population.Age, age => Assert.Equal(0, age));

        // The deterministic path still draws it, and still spreads the first cohort.
        var calendar = Households.Draw(Defaults with { Run = Defaults.Run with { Households = 400 } }, new GoodsTable(Defaults), runSeed: 1);

        Assert.Contains(calendar.Age, age => age > 1);
    }

    // ---- the unconditional draw ----------------------------------------------------------------

    /// <summary>One run's step-5 transitions: what was held going in, and what was lost.</summary>
    private sealed record Log(bool[][] HeldBefore, bool[][] Lost);

    private static Log RunAndLog(SimulationParameters parameters, int seed)
    {
        var simulation = new Simulation(parameters, seed);

        Assert.True(simulation.Start().IsSuccess);

        var cells = parameters.Run.Households * simulation.Goods.CategoryCount;
        var heldBefore = new List<bool[]>();
        var lost = new List<bool[]>();
        bool[]? before = null;

        simulation.StepObserver = step =>
        {
            // The observer fires *before* each step, so this is the state going into step 5 and,
            // one step later, the state coming out of it.
            if (step == TickStep.Ageing)
            {
                before = (bool[])simulation.Population.Holds.Clone();
            }
            else if (step == TickStep.Repricing && before is not null)
            {
                var after = simulation.Population.Holds;
                var went = new bool[cells];

                for (var i = 0; i < cells; i++)
                {
                    went[i] = before[i] && !after[i];
                }

                heldBefore.Add(before);
                lost.Add(went);
                before = null;
            }
        };

        for (var tick = 1; tick <= parameters.Run.Ticks; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);
        }

        return new Log([.. heldBefore], [.. lost]);
    }

    /// <summary>
    /// The failure stream replayed from outside the model: for every household, good and tick, in
    /// the order the model consumes it, whether the draw is below `1 / life`.
    /// </summary>
    private static bool[][] Replay(int seed, GoodsTable goods, int households, int ticks)
    {
        var schedule = new bool[ticks][];

        for (var t = 0; t < ticks; t++)
        {
            schedule[t] = new bool[households * goods.CategoryCount];
        }

        for (var h = 0; h < households; h++)
        {
            var stream = RandomStream.ForHousehold(seed, h, Purpose.Failure);

            for (var t = 0; t < ticks; t++)
            {
                for (var c = 0; c < goods.CategoryCount; c++)
                {
                    schedule[t][(h * goods.CategoryCount) + c] =
                        stream.NextDouble() < 1.0 / goods.Categories[c].Life;
                }
            }
        }

        return schedule;
    }

    /// <summary>
    /// **The criterion this story exists to protect.** Two arms that ration differently consume the
    /// identical stream, so a household's failure months are a property of the household and the
    /// seed rather than of what it managed to buy.
    ///
    /// A household with no unit cannot fail, so drawing for it looks like waste. Skip it and the
    /// stream position depends on who was rationed — which differs between arms *by construction*,
    /// since being rationed is the thing under measurement. The two arms then sit on different
    /// worlds, every household is a different household, and the paired comparison keeps returning
    /// plausible numbers. 02-04 had this argument about `θ` and 10-02 had it about the archetype
    /// assignment; this is its third and least obvious instance, because here the *state* that would
    /// gate the draw is itself an outcome.
    ///
    /// The test replays the stream from outside the model and requires every loss, in both arms, to
    /// be exactly "held, and the stream said so". A conditional draw desynchronises the replay in
    /// the arm with more rationing and this fails; nothing else would notice.
    /// </summary>
    [Fact]
    public void TheFailureDrawIsUnconditional_SoDifferentlyRationedArmsStayOnOneWorld()
    {
        const int Seed = 3;

        var off = Hazard(credit: false);
        var high = Hazard(credit: true);

        var offLog = RunAndLog(off, Seed);
        var highLog = RunAndLog(high, Seed);

        var goods = new GoodsTable(off);
        var schedule = Replay(Seed, goods, off.Run.Households, off.Run.Ticks);

        Assert.Equal(off.Run.Ticks, offLog.Lost.Length);

        // The arms really do hold different things, or this test would pass on a broken model.
        var different = 0;

        for (var t = 0; t < off.Run.Ticks; t++)
        {
            for (var i = 0; i < offLog.HeldBefore[t].Length; i++)
            {
                if (offLog.HeldBefore[t][i] != highLog.HeldBefore[t][i])
                {
                    different++;
                }
            }
        }

        Assert.True(different > 0, "the two arms held identical stock, so nothing about rationing was exercised");

        foreach (var (log, arm) in new[] { (offLog, "credit_off"), (highLog, "credit_high") })
        {
            for (var t = 0; t < off.Run.Ticks; t++)
            {
                for (var i = 0; i < schedule[t].Length; i++)
                {
                    Assert.True(
                        log.Lost[t][i] == (log.HeldBefore[t][i] && schedule[t][i]),
                        Invariant($"{arm}, tick {t + 1}, cell {i}: the stream and the model disagree about a failure"));
                }
            }
        }
    }

    // ---- the life-1 special case, gone ---------------------------------------------------------

    /// <summary>
    /// `p = 1 / life` is 1 at `life = 1`, so a consumable is consumed every tick by the same rule
    /// that fails a washing machine. `wants = life == 1 || age ≥ life` becomes one condition on one
    /// flag, and this asserts the collapse rather than trusting it.
    /// </summary>
    [Fact]
    public void ALifeOneGoodIsConsumedEveryTickByTheHazardItself()
    {
        var parameters = Hazard();
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters, goods, runSeed: 5);

        var lifeOne = Enumerable.Range(0, goods.CategoryCount).Where(c => goods.Categories[c].Life == 1).ToArray();

        Assert.NotEmpty(lifeOne);

        for (var tick = 0; tick < 25; tick++)
        {
            // Everything working going in; only the life-1 goods must be gone coming out.
            Array.Fill(population.Holds, true);
            population.FailDurables(goods);

            foreach (var c in lifeOne)
            {
                for (var h = 0; h < population.Count; h++)
                {
                    Assert.False(population.Holds[population.AgeIndex(h, c)]);
                }
            }
        }

        // And the want that follows is the same want the calendar produced: every tick, for every
        // household, without a branch on `life == 1` anywhere in the hazard path.
        for (var h = 0; h < population.Count; h++)
        {
            foreach (var c in lifeOne)
            {
                population.RefreshWantFromHolding(h, c);

                Assert.True(population.Wanted[population.AgeIndex(h, c)]);
            }
        }
    }

    // ---- what the hazard costs, and what it buys -----------------------------------------------

    /// <summary>
    /// Unconstrained replacement: every failure is replaced at once, so what is left is the failure
    /// process on its own.
    /// </summary>
    private static double[][] UnconstrainedFailures(SimulationParameters parameters, int seed, int ticks)
    {
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters, goods, seed);
        var counts = new double[goods.CategoryCount][];

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            counts[c] = new double[ticks];
        }

        for (var t = 0; t < ticks; t++)
        {
            population.FailDurables(goods);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                var failed = 0;

                for (var h = 0; h < population.Count; h++)
                {
                    if (!population.Holds[population.AgeIndex(h, c)])
                    {
                        failed++;
                    }
                }

                counts[c][t] = failed;
            }

            Array.Fill(population.Holds, true);
        }

        return counts;
    }

    /// <summary>
    /// **The first tick is already the steady state.** A fraction `1 / life` of every good fails in
    /// tick 1, so the count is `capacity_g` within sampling error and there is no warm-up to serve.
    ///
    /// That is the property the calendar could not have. Deterministic replacement separates its
    /// opening cohorts once, at initialisation, and then never mixes them — a household replacing in
    /// month 7 replaces in month 7 + life forever, and only rationing decorrelates them.
    /// Memorylessness does it for free.
    /// </summary>
    [Fact]
    public void TheFirstTicksFailuresAreAlreadyCapacity()
    {
        const int Seeds = 20;

        var parameters = Hazard(households: 5000, basis: Grouped());
        var goods = new GoodsTable(parameters);
        var totals = new double[goods.CategoryCount];

        // Across seeds rather than within one. A single tick of `Binomial(5000, 1/4)` has a
        // standard deviation of 30 units, so one draw says almost nothing about where the process
        // is centred — clothing basics came in at 1,380 against 1,250 on seed 11, which is an
        // ordinary tick and would have been read here as a broken opening.
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var first = UnconstrainedFailures(parameters, seed, ticks: 1);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                totals[c] += first[c][0];
            }
        }

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            var p = 1.0 / parameters.Categories[c].Life;
            var expected = parameters.Run.Households * p;
            var error = Math.Sqrt(parameters.Run.Households * p * (1.0 - p) / Seeds);

            Assert.Equal(parameters.Categories[c].Capacity, (int)Math.Round(expected, MidpointRounding.AwayFromZero));
            Assert.InRange(totals[c] / Seeds, expected - (4.0 * error), expected + (4.0 * error));
        }
    }

    /// <summary>
    /// Replacement demand is stationary, and the price of that is per-tick noise: `Binomial(N, p)`
    /// rather than a near-constant.
    ///
    /// The cost is stated so nobody discovers it. The relative per-tick spread is `√((1−p)/(N·p))`,
    /// which at 5,000 households is about 17% for large appliances and 2.5% for clothing basics —
    /// but over a 360-tick measured window it is that divided by √360, under a per cent for every
    /// good in the table. That is why the hazard is affordable at all, and it is also why the
    /// figure has to be quoted with its window rather than alone.
    /// </summary>
    [Fact]
    public void ReplacementDemandIsStationaryAndItsSpreadIsTheBinomialOne()
    {
        const int Ticks = 400;
        const int Window = 360;

        var parameters = Hazard(households: 5000, basis: Grouped());
        var goods = new GoodsTable(parameters);
        var counts = UnconstrainedFailures(parameters, seed: 11, Ticks);

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            var series = counts[c].AsSpan(Ticks - Window);
            var life = parameters.Categories[c].Life;
            var p = 1.0 / life;
            var expected = parameters.Run.Households * p;

            var mean = 0.0;

            foreach (var value in series)
            {
                mean += value;
            }

            mean /= Window;

            var variance = 0.0;

            foreach (var value in series)
            {
                variance += (value - mean) * (value - mean);
            }

            variance /= Window - 1;

            var relative = Math.Sqrt(variance) / mean;
            var predicted = Math.Sqrt((1.0 - p) / (parameters.Run.Households * p));

            // The mean is capacity, to within four standard errors of a 360-tick window.
            Assert.InRange(
                mean,
                expected - (4.0 * Math.Sqrt(variance / Window)),
                expected + (4.0 * Math.Sqrt(variance / Window)));

            // The spread is the binomial one. A quarter either way: the estimate of a standard
            // deviation from 360 points carries about 4% relative error itself, and this is a check
            // that the process is what it claims to be rather than a measurement of it.
            Assert.InRange(relative, predicted * 0.75, predicted * 1.25);

            // Over the window, under a per cent — which is the number that matters, because the
            // window is what a result is read off.
            Assert.True(
                relative / Math.Sqrt(Window) < 0.01,
                Invariant($"{parameters.Categories[c].Name}: {relative / Math.Sqrt(Window):P2} over {Window} ticks"));

            // Stationary: the two halves of the window agree.
            var half = Window / 2;
            var early = Mean(series[..half]);
            var late = Mean(series[half..]);

            Assert.InRange(late - early, -4.0 * Math.Sqrt(2.0 * variance / half), 4.0 * Math.Sqrt(2.0 * variance / half));
        }
    }

    /// <summary>The same, under the calendar: everyone due this tick buys, then everything ages.</summary>
    private static double[][] UnconstrainedReplacements(SimulationParameters parameters, int seed, int ticks)
    {
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters with { Run = parameters.Run with { Replacement = Replacement.Deterministic } }, goods, seed);
        var counts = new double[goods.CategoryCount][];

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            counts[c] = new double[ticks];
        }

        for (var t = 0; t < ticks; t++)
        {
            for (var c = 0; c < goods.CategoryCount; c++)
            {
                var due = 0;

                for (var h = 0; h < population.Count; h++)
                {
                    population.RefreshWant(h, c, parameters.Categories[c].Life);

                    if (population.Wanted[population.AgeIndex(h, c)])
                    {
                        population.Acquire(h, c);
                        due++;
                    }
                }

                counts[c][t] = due;
            }

            population.AgeDurables(goods);
        }

        return counts;
    }

    /// <summary>
    /// **What the hazard actually buys, measured — and it is not less noise. Found 2026-09-04.**
    ///
    /// An earlier draft of `01-SIMULATION.md` §5.5 claimed a four-month good replaced on the
    /// calendar "oscillates with a per-tick standard deviation of 500 units about a mean that never
    /// reaches capacity", against 31 for the hazard, and concluded that V4 should get *easier*.
    /// Measured on an unconstrained population of 5,000 the calendar sits at 1250.0 ± 35.8 and the
    /// hazard at 1251.8 ± 31.6 — the same spread, and the calendar's mean is capacity exactly.
    ///
    /// The difference is **structure, not size**. The calendar's series is periodic: its cohorts are
    /// fixed at initialisation and never mix, so the whole variance is four numbers repeating, and
    /// its autocorrelation at lag `life` is essentially 1. The hazard is memoryless and its
    /// autocorrelation at any lag is essentially 0. That is what makes the hazard worth having —
    /// there is no cohort to initialise and none to warm out — and it is why the null run should be
    /// expected to behave about the same rather than better.
    /// </summary>
    [Fact]
    public void TheHazardIsNotQuieterThanTheCalendar_ItIsAperiodic()
    {
        const int Ticks = 400;
        const int Window = 360;

        var parameters = Hazard(households: 5000, basis: Grouped());
        var goods = new GoodsTable(parameters);
        var hazard = UnconstrainedFailures(parameters, seed: 11, Ticks);
        var calendar = UnconstrainedReplacements(parameters, seed: 11, Ticks);

        foreach (var name in new[] { "clothing_basics", "appliance_large" })
        {
            var c = Enumerable.Range(0, goods.CategoryCount).Single(i => parameters.Categories[i].Name == name);
            var life = parameters.Categories[c].Life;

            var h = hazard[c].AsSpan(Ticks - Window);
            var k = calendar[c].AsSpan(Ticks - Window);

            // Both centred on capacity, and neither materially noisier than the other.
            Assert.InRange(Mean(k), parameters.Categories[c].Capacity - 1.0, parameters.Categories[c].Capacity + 1.0);
            Assert.InRange(Spread(h) / Spread(k), 0.7, 1.4);

            // The calendar repeats itself every `life` ticks; the hazard remembers nothing.
            Assert.InRange(Autocorrelation(k, life), 0.9, 1.0);
            Assert.InRange(Autocorrelation(h, life), -0.2, 0.2);
        }
    }

    private static double Spread(ReadOnlySpan<double> values)
    {
        var mean = Mean(values);
        var variance = 0.0;

        foreach (var value in values)
        {
            variance += (value - mean) * (value - mean);
        }

        return Math.Sqrt(variance / (values.Length - 1));
    }

    /// <summary>
    /// The correlation between a series and itself `lag` ticks later, over the overlapping part.
    ///
    /// Normalised by the two overlapping segments rather than by the whole series: the textbook
    /// estimator divides by the full sum of squares and is therefore biased down by
    /// `(n − lag) / n`, which at a lag of 144 over 360 ticks is a factor of 0.6 — enough to read a
    /// perfectly periodic series as barely correlated.
    /// </summary>
    private static double Autocorrelation(ReadOnlySpan<double> values, int lag)
    {
        var mean = Mean(values);
        var above = 0.0;
        var left = 0.0;
        var right = 0.0;

        for (var i = 0; i + lag < values.Length; i++)
        {
            var a = values[i] - mean;
            var b = values[i + lag] - mean;

            above += a * b;
            left += a * a;
            right += b * b;
        }

        return left == 0.0 || right == 0.0 ? 0.0 : above / Math.Sqrt(left * right);
    }

    /// <summary>A hazard run completes: seven steps, money conservation and all, on both calibrations.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHazardRunCompletes(bool grouped)
    {
        var parameters = Hazard(households: 600, ticks: 60, basis: grouped ? Grouped() : Defaults);
        var simulation = new Simulation(parameters, runSeed: 4);

        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= parameters.Run.Ticks; tick++)
        {
            var ran = simulation.RunTick(tick);

            // Step 7 halts on a violation rather than warning, so a completed run is the
            // conservation identity holding to the cent in every tick of it.
            Assert.True(ran.IsSuccess, ran.IsFailed ? ran.Errors[0].Message : "");
        }

        // And something was actually bought and worn out, or the run proves nothing.
        Assert.Contains(simulation.Population.Holds, held => !held);
        Assert.True(simulation.Cohorts.Obtained(Cohort.Abstainer) > 0);
    }

    private static double Mean(ReadOnlySpan<double> values)
    {
        var total = 0.0;

        foreach (var value in values)
        {
            total += value;
        }

        return total / values.Length;
    }

    private static SimulationParameters Grouped()
    {
        var loaded = ConfigurationLoader.FromFile(
            Path.Combine(Repo.Root, "config", "calibrations", "grouped.toml"));

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        return loaded.Value;
    }
}
