using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;
using static System.FormattableString;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/11-04. Each archetype replaces each good on its own cycle: `d[A][g]` multiplies
/// the good's life, so a type differs in how often it turns up at the shelf and not only in what it
/// will pay when it does.
///
/// **Two identities, conserving two different things.** `Σ_A share_A · m[A][g] = 1` fixes the score
/// level, so §3.6's base scores keep meaning what they say at the population mean.
/// `Σ_A share_A / d[A][g] = 1` fixes the units, so `capacity = round(households / life_g)` stays
/// correct as written. Neither implies the other, a table can satisfy one while breaking the other,
/// and the second is harmonic for a reason that is arithmetic rather than stylistic — which is what
/// most of this file is about.
/// </summary>
public sealed class ReplacementCycleTests
{
    private const string Phone = "phone";
    private const string Electronics = "electronics";

    private static readonly string[] Types =
        ["family_practical", "gadget", "health_conscious", "prudent"];

    // ---- loading -----------------------------------------------------------------------------

    private static SimulationParameters Grouped() => Load(Read("grouped.toml"), SimulationParameters.Default);

    private static SimulationParameters Cycles() => Load(Read("grouped_cycles.toml"), Grouped());

    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(Repo.Root, "config", "calibrations", file));

    private static Archetype Type(SimulationParameters parameters, string name) =>
        parameters.Archetypes.Types.Single(t => string.Equals(t.Name, name, StringComparison.Ordinal));

    /// <summary>The grouped calibration, sized for a town, with the cycle table on top.</summary>
    private static SimulationParameters Town(int households) =>
        Sized(Cycles(), households);

    private static SimulationParameters Sized(SimulationParameters parameters, int households) =>
        parameters.WithHouseholds(households);

    // ---- the file loads at all ---------------------------------------------------------------

    /// <summary>
    /// The overlay loads on the grouped table, keeps its eighteen rows, and states a cycle for
    /// exactly the twelve durables §3.7 names.
    /// </summary>
    [Fact]
    public void TheCycleTable_LoadsOnTheGroupedCalibration()
    {
        var parameters = Cycles();

        Assert.Equal(18, parameters.Categories.Count);
        Assert.Equal(Replacement.Hazard, parameters.Run.Replacement);
        Assert.Equal(Types, parameters.Archetypes.Types.Select(t => t.Name).ToArray());

        // Every row of the goods table has an entry, because absent means 1.0 and the loader
        // records the whole column; the ones that differ from 1 are §3.7's twelve.
        foreach (var type in parameters.Archetypes.Types)
        {
            Assert.Equal(18, type.D.Count);
        }

        var moved = parameters.Categories
            .Where(g => parameters.Archetypes.Types.Any(t => t.CycleFor(g.Name) != 1.0))
            .Select(g => g.Name)
            .ToArray();

        Assert.Equal(12, moved.Length);
        Assert.All(moved, name => Assert.True(parameters.Categories.Single(c => c.Name == name).Life > 1));
    }

    /// <summary>
    /// Taste is authored per **category label**, the cycle per **good**, and the two levels are not
    /// interchangeable.
    ///
    /// §3.5's table says a type wants electronics; §3.7's says it churns a phone and keeps its
    /// television. Under §3.1's six rows a label is a row and the distinction is invisible — which
    /// is the whole reason every file written before E11 still means what it meant — so it can only
    /// be tested on a table where the two differ.
    /// </summary>
    [Fact]
    public void TasteIsAuthoredPerLabel_AndTheCyclePerGood()
    {
        var parameters = Cycles();
        var gadget = Type(parameters, "gadget");

        // Six labels for taste, eighteen rows for the cycle.
        Assert.Equal(6, gadget.W.Count);
        Assert.Equal(6, gadget.Kappa.Count);
        Assert.Equal(18, gadget.D.Count);

        // One taste for electronics, three different cycles inside it.
        Assert.Equal(gadget.WeightFor(Electronics), gadget.WeightFor(Electronics));
        Assert.NotEqual(gadget.CycleFor(Phone), gadget.CycleFor("tv"));
        Assert.NotEqual(gadget.CycleFor(Phone), gadget.CycleFor("laptop"));

        // And the levels do not accept each other's keys.
        var wByGood = ConfigurationLoader.FromToml(
            "[archetypes.only]\nshare = 1.0\nw = { phone = 1.6 }\n",
            Grouped());

        Assert.True(wByGood.IsFailed);
        Assert.Contains(wByGood.Errors, e => e.Message.Contains("archetypes.only.w", StringComparison.Ordinal));

        var dByLabel = ConfigurationLoader.FromToml(
            "[run]\nreplacement = \"hazard\"\n[archetypes.only]\nshare = 1.0\nd = { electronics = 0.7 }\n",
            Grouped());

        Assert.True(dByLabel.IsFailed);
        Assert.Contains(dByLabel.Errors, e => e.Message.Contains("archetypes.only.d", StringComparison.Ordinal));
    }

    // ---- the harmonic normalisation, which is the story ---------------------------------------

    /// <summary>
    /// `Σ_A share_A / d[A][g] = 1` on the normalised table, for every one of the eighteen goods.
    ///
    /// This is the identity that keeps `capacity = round(households / life_g)` correct. Demand per
    /// tick is `1 / life`, so what has to average to one across the population is the reciprocal.
    /// </summary>
    [Fact]
    public void TheNormalisedCycleTable_HasAHarmonicMeanOfOne()
    {
        var parameters = Cycles();

        foreach (var good in parameters.Categories)
        {
            var reciprocal = parameters.Archetypes.Types.Sum(t => t.Share / t.CycleFor(good.Name));

            Assert.Equal(1.0, reciprocal, 12);
        }
    }

    /// <summary>
    /// **The test the story asks for by name.** A table whose *arithmetic* mean is already 1 is the
    /// bug, and this is what catches it.
    ///
    /// Take §3.7's phone column and rescale it so `Σ share · d = 1` — the normalisation somebody
    /// reaches for because it is the one `w` gets. Jensen's inequality then says
    /// `Σ share / d > 1` strictly, for any `d` that varies at all: the population replaces more
    /// often than the shelf was sized for. Here it is 6.7% more, permanently, in that good, in
    /// every run — and nothing else in this repository fires, because the reprice rule absorbs a
    /// permanently tight shelf into the price level and every conservation identity still holds to
    /// the cent.
    ///
    /// The loader is handed exactly that table and has to fix it. Both assertions matter: that the
    /// arithmetic table is wrong in the direction Jensen predicts, and that what comes out is right.
    /// </summary>
    [Fact]
    public void AnArithmeticallyNormalisedCycleTable_IsRenormalised_AndWasBuyingTooMuch()
    {
        var parameters = Cycles();
        var shares = parameters.Archetypes.Types.ToDictionary(t => t.Name, t => t.Share, StringComparer.Ordinal);

        // §3.7's authored phone column, as the file states it.
        var authored = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["prudent"] = 1.45,
            ["health_conscious"] = 1.00,
            ["gadget"] = 0.70,
            ["family_practical"] = 1.10,
        };

        var arithmeticMean = authored.Sum(a => shares[a.Key] * a.Value);
        var arithmetic = authored.ToDictionary(a => a.Key, a => a.Value / arithmeticMean, StringComparer.Ordinal);

        // It satisfies the wrong identity exactly ...
        Assert.Equal(1.0, arithmetic.Sum(a => shares[a.Key] * a.Value), 12);

        // ... and overshoots the right one, in the direction Jensen guarantees.
        var overshoot = arithmetic.Sum(a => shares[a.Key] / a.Value);

        Assert.True(overshoot > 1.0, Invariant($"Σ share / d came to {overshoot:F6}"));
        Assert.Equal(1.0667, overshoot, 4);

        // Hand the loader that table and it comes back harmonically normalised.
        var toml = "[run]\nreplacement = \"hazard\"\n" + string.Join(
            "",
            arithmetic.Select(a => Invariant(
                $"[archetypes.{a.Key}]\nshare = {shares[a.Key]}\nd = {{ phone = {a.Value} }}\n")));

        var reloaded = Load(toml);

        foreach (var good in reloaded.Categories)
        {
            Assert.Equal(1.0, reloaded.Archetypes.Types.Sum(t => t.Share / t.CycleFor(good.Name)), 12);
        }

        // Same shape as the authored table — the relative cycles are untouched, only the level moved.
        var ratio = Type(reloaded, "gadget").CycleFor(Phone) / Type(reloaded, "prudent").CycleFor(Phone);

        Assert.Equal(0.70 / 1.45, ratio, 12);
    }

    /// <summary>
    /// The consequence of the previous test, measured on the failure process rather than argued
    /// from the algebra: realised replacement demand per tick matches `capacity_g`.
    ///
    /// This is what a wrong normalisation breaks and, as the story says, nothing else catches. The
    /// arithmetic table is run alongside so the test says how much it would have missed by — the
    /// point being that "a bit more demand than capacity" is not a number any invariant reports.
    /// </summary>
    [Fact]
    public void RealisedReplacementDemand_MatchesTheCapacityTheShelfWasSizedFor()
    {
        const int Households = 5000;
        const int Ticks = 400;
        const int Window = 360;

        var parameters = Town(Households);
        var goods = new GoodsTable(parameters);
        var counts = UnconstrainedFailures(parameters, seed: 7, Ticks);

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            var series = counts[c].AsSpan(Ticks - Window);
            var mean = Mean(series);
            var capacity = parameters.Categories[c].Capacity;

            // Four standard errors of a 360-tick window of Binomial(N, 1/life) draws, plus the
            // rounding in `capacity` itself, which is up to half a unit and matters where the
            // shelf is ten units wide.
            var p = 1.0 / parameters.Categories[c].Life;
            var error = (4.0 * Math.Sqrt(Households * p * (1.0 - p) / Window)) + 0.5;

            Assert.InRange(mean, capacity - error, capacity + error);
        }
    }

    // ---- the two identities, and that they are different --------------------------------------

    /// <summary>
    /// `Σ_A share_A · m[A][g] = 1` holds on the authored score multiplier, and does **not** hold on
    /// the derived taste weight `ŵ = m / d`. The share-weighted mean of `ŵ` is not 1 and is not
    /// meant to be.
    ///
    /// Two identities, two different things conserved. Read the failure of the second as the model
    /// saying that a population which replaces at different rates does not value a good-month at
    /// the same average as one that does not — which is exactly right, and is why deriving `ŵ`
    /// rather than authoring it is the whole of §3.7's arithmetic.
    /// </summary>
    [Fact]
    public void TheScoreMultiplierAveragesToOne_AndTheDerivedTasteWeightDoesNot()
    {
        var parameters = Cycles();
        var types = parameters.Archetypes.Types;
        var missed = 0;

        foreach (var good in parameters.Categories)
        {
            Assert.Equal(1.0, types.Sum(t => t.Share * t.WeightFor(good.Label)), 12);

            var derived = types.Sum(t => t.Share * t.TasteWeightFor(good.Label, good.Name));
            var hasCycle = types.Any(t => t.CycleFor(good.Name) != 1.0);

            if (!hasCycle)
            {
                // `d = 1`, so `ŵ = m` and the second identity holds here too. Six of the eighteen,
                // and the same reason E10's tables are untouched by any of this.
                Assert.Equal(1.0, derived, 12);
                continue;
            }

            // Strictly above 1, in every good with a cycle, without exception — `E[m/d]` exceeds
            // `E[m]·E[1/d]` by the covariance between wanting a good and churning it, and both are
            // positive here by construction. It runs from 1.0098 on medium appliances to 1.0715 on
            // the phone, so a magnitude threshold would be a threshold and this is a direction.
            Assert.True(derived > 1.0, Invariant($"{good.Name}: Σ share · ŵ came to {derived:F6}"));
            missed++;
        }

        Assert.Equal(12, missed);
    }

    /// <summary>
    /// §3.7's worked case, to three decimals: `gadget`'s phone.
    ///
    /// `d' = 0.676` after normalisation, `m = 1.553` after normalisation, and therefore
    /// `ŵ = 2.299`. The weight is more than twice the average and that is not a defect: this
    /// household bids 1.553× on a phone while buying phones 48% more often, so it values a
    /// phone-month at more than twice what the town does. Those are competing claims on one budget
    /// and the model is entitled to say so.
    /// </summary>
    [Fact]
    public void TheWorkedCase_GadgetsPhone()
    {
        var parameters = Cycles();
        var gadget = Type(parameters, "gadget");
        var phone = parameters.Categories.Single(c => c.Name == Phone);

        Assert.Equal(0.676, gadget.CycleFor(Phone), 3);
        Assert.Equal(1.553, gadget.WeightFor(Electronics), 3);
        Assert.Equal(2.299, gadget.TasteWeightFor(phone.Label, phone.Name), 3);

        // The realised life, which is the number §3.7's table prints: twenty months and a bit.
        Assert.Equal(20.3, phone.Life * gadget.CycleFor(Phone), 1);

        // And `prudent`, at the other end, keeps a phone three and a half years.
        Assert.Equal(42.0, phone.Life * Type(parameters, "prudent").CycleFor(Phone), 1);
    }

    /// <summary>
    /// The composition, measured through the engine rather than asserted on the table: a `gadget`
    /// household's realised score multiplier on a phone is **1.553**, not 1.050.
    ///
    /// `flow_cost` divides by the household's own life, so the score is proportional to `ŵ · d` and
    /// the two tables multiply back to `m`. Author `ŵ = 1.553` directly instead — the obvious
    /// reading of §3.5's table — and the same division takes it to `1.553 × 0.676 = 1.050`: almost
    /// all of the taste meant to make this type the top phone bidder is spent paying for the churn,
    /// and `prudent`, at 0.777 × 1.400 = 1.087, comes out bidding *higher*. That is the opposite of
    /// what the table says, and it is why the weight is derived.
    /// </summary>
    [Fact]
    public void TheScoreMultiplierSurvivesTheCycle_BecauseTheWeightIsDerived()
    {
        var parameters = Town(2000);
        var goods = new GoodsTable(parameters);
        var population = Households.Draw(parameters, goods, runSeed: 3);
        var phone = Array.FindIndex([.. goods.Categories], c => c.Name == Phone);

        var byType = new Dictionary<string, double>(StringComparer.Ordinal);

        for (var h = 0; h < population.Count; h++)
        {
            var name = population.ArchetypeNames[population.Archetype[h]];

            if (byType.ContainsKey(name))
            {
                continue;
            }

            // What this household's score is a multiple of, against the same household under the
            // identity table: its taste is `w_h · ŵ · ε` where that household's would be `w_h`
            // (σ_idio is 0, so ε is exactly 1), and it pays over `life_h` where that one pays over
            // the good's own life. Score is `Δvalue / Δcost`, so the two factors multiply.
            var taste = population.Taste(h, phone) / population.TasteWeight[h];
            var life = population.Life(h, phone) / goods.Categories[phone].Life;

            byType[name] = taste * life;
        }

        Assert.Equal(4, byType.Count);

        // The realised multiplier is the authored score multiplier, for every type.
        foreach (var type in parameters.Archetypes.Types)
        {
            Assert.Equal(type.WeightFor(Electronics), byType[type.Name], 9);
        }

        Assert.Equal(1.553, byType["gadget"], 3);

        // And the counterfactual the story names: authoring `ŵ` directly would have left `gadget`
        // below `prudent` on the very good it is defined by wanting.
        var authoredDirectly = parameters.Archetypes.Types.ToDictionary(
            t => t.Name,
            t => t.WeightFor(Electronics) * t.CycleFor(Phone),
            StringComparer.Ordinal);

        Assert.Equal(1.050, authoredDirectly["gadget"], 3);
        Assert.Equal(1.087, authoredDirectly["prudent"], 3);
        Assert.True(authoredDirectly["gadget"] < authoredDirectly["prudent"]);
    }

    // ---- what a cycle may not do --------------------------------------------------------------

    /// <summary>
    /// A `d` under `replacement = "deterministic"` is **rejected, never rounded**.
    ///
    /// A deterministic life is an integer count of ticks, so a realised 20.3 months has nowhere to
    /// land. Rounding it per archetype is the tempting accommodation and it breaks the units
    /// identity by more than an arithmetic normalisation would — 1.040 on clothing basics against
    /// 1.025 — while a load-time assertion on the unrounded table goes on passing. Deferred here
    /// from 11-03, which had no `d` to reject.
    /// </summary>
    [Fact]
    public void ACycleUnderTheDeterministicRule_IsRejectedRatherThanRounded()
    {
        var calendar = Read("grouped_cycles.toml").Replace("replacement = \"hazard\"", "", StringComparison.Ordinal);
        var loaded = ConfigurationLoader.FromToml(calendar, Grouped());

        Assert.True(loaded.IsFailed);

        var complaints = loaded.Errors.Select(e => e.Message).ToArray();

        Assert.Contains(complaints, m => m.Contains("archetypes.d.phone", StringComparison.Ordinal));
        Assert.Contains(complaints, m => m.Contains("hazard", StringComparison.Ordinal));

        // One complaint per good, not one per (type, good): normalisation is per column, so the
        // moment one type states a cycle every other type's 1.0 is scaled off 1 as well.
        Assert.Equal(12, complaints.Count(m => m.Contains("archetypes.d.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A `d` on a life-1 good fails to load rather than being silently ignored.
    ///
    /// Groceries are consumed the tick they are bought — `p = 1 / life` is 1 — so there is no cycle
    /// to stretch and a multiplier there is a statement the model cannot carry out.
    /// </summary>
    [Fact]
    public void ACycleOnALifeOneGood_FailsToLoad()
    {
        var loaded = ConfigurationLoader.FromToml(
            "[run]\nreplacement = \"hazard\"\n"
            + "[archetypes.thrifty]\nshare = 0.5\nd = { groceries = 1.2 }\n"
            + "[archetypes.other]\nshare = 0.5\n",
            Grouped());

        Assert.True(loaded.IsFailed);
        Assert.Contains(
            loaded.Errors,
            e => e.Message.Contains("archetypes.d.groceries", StringComparison.Ordinal)
                 && e.Message.Contains("life 1", StringComparison.Ordinal));
    }

    /// <summary>
    /// A cycle that would take a realised life below one tick fails to load.
    ///
    /// `1 / life` is a probability and above 1 it stops being one: the good would fail every tick
    /// whatever it says, quietly becoming a consumable with a durable's price.
    ///
    /// The check is on the **normalised** table, because that is the life the run would use — and
    /// the normalisation is most of the reason this is hard to trip. A tenth of the population on
    /// `d = 0.001` is not a tenth of the population replacing a thousand times as often: the column
    /// is scaled up by `Σ share / d`, which that type dominates, so it lands on 0.10 and everybody
    /// else on 100. It takes a small minority with an extreme cycle, which is exactly the shape
    /// somebody writes when they mean "these people replace constantly".
    /// </summary>
    [Fact]
    public void ACycleShorterThanATick_FailsToLoad()
    {
        var loaded = ConfigurationLoader.FromToml(
            "[run]\nreplacement = \"hazard\"\n"
            + "[archetypes.churner]\nshare = 0.1\nd = { clothing_basics = 0.001 }\n"
            + "[archetypes.other]\nshare = 0.9\n",
            Grouped());

        Assert.True(loaded.IsFailed);
        Assert.Contains(
            loaded.Errors,
            e => e.Message.Contains("archetypes.d.clothing_basics", StringComparison.Ordinal)
                 && e.Message.Contains("at least one tick", StringComparison.Ordinal));
    }

    /// <summary>A cycle of zero is a division by zero one step later, and is caught as one.</summary>
    [Fact]
    public void ACycleOfZero_FailsToLoad()
    {
        var loaded = ConfigurationLoader.FromToml(
            "[run]\nreplacement = \"hazard\"\n"
            + "[archetypes.broken]\nshare = 0.5\nd = { phone = 0.0 }\n"
            + "[archetypes.other]\nshare = 0.5\n",
            Grouped());

        Assert.True(loaded.IsFailed);
        Assert.Contains(loaded.Errors, e => e.Message.Contains("archetypes.d.phone", StringComparison.Ordinal));
    }

    // ---- capacity, and the residue that must not be corrected ---------------------------------

    /// <summary>
    /// `capacity = round(households / life_g)` is unchanged, and is **not** derived from the
    /// realised archetype assignment: two seeds of one scenario produce identical effective
    /// configurations.
    ///
    /// Deriving it from the population that was actually drawn is the rigorous-looking fix for the
    /// finite-sample residue and it is the trap. The goods table becomes a function of the seed,
    /// and the campaign collector then refuses a scenario whose seeds ran different effective
    /// configurations — correctly, because a seed that changes a parameter has become a parameter.
    /// </summary>
    [Fact]
    public void CapacityIsNotDerivedFromTheAssignment_SoTwoSeedsShareOneConfiguration()
    {
        var parameters = Town(5000);

        // The configuration does not depend on a seed at all; what the seed changes is the
        // population, and two of them are drawn here to say so.
        var first = new Simulation(parameters, runSeed: 1);
        var second = new Simulation(parameters, runSeed: 2);

        Assert.Equal(first.Parameters.ToToml(), second.Parameters.ToToml());

        foreach (var good in parameters.Categories)
        {
            Assert.Equal(
                SimulationParameters.DerivedCapacity(parameters.Run.Households, good.Life),
                good.Capacity);
        }

        // The two populations really are different, so the equality above is a claim rather than a
        // tautology about two copies of one object.
        Assert.NotEqual<IEnumerable<int>>(
            first.Population.Archetype.Take(50).ToArray(),
            second.Population.Archetype.Take(50).ToArray());
    }

    /// <summary>
    /// The finite-sample residue is real, is small, and is **reported once rather than corrected**
    /// (§3.7).
    ///
    /// Two sources and they are different sizes. The assignment: 5,000 households split four ways
    /// by share land off nominal by half a percentage point or so, which moves a good's true
    /// replacement demand against the capacity it was sized for. And `round`: at 1,000 households
    /// an appliance shelf of `1000 / 96 = 10.4` rounds to 10, which is 4.2% on its own — the larger
    /// of the two wherever the shelf is only a few units wide, and the argument for §3.6's bigger
    /// town rather than for a cleverer capacity rule.
    /// </summary>
    [Fact]
    public void TheResidueIsReported_AndIsUnderAPerCentWhereTheShelfIsWideEnoughToRoundCleanly()
    {
        var parameters = Town(5000);
        var simulation = new Simulation(parameters, runSeed: 4);
        var (good, relative) = simulation.ReplacementResidue;

        Assert.NotEqual("", good);
        Assert.True(Math.Abs(relative) < 0.02, Invariant($"{good}: {relative:P2}"));

        // Where the rounding is not what dominates — a shelf of more than a hundred units — the
        // assignment residue on its own is well under a per cent.
        for (var c = 0; c < parameters.Categories.Count; c++)
        {
            if (parameters.Categories[c].Capacity < 100)
            {
                continue;
            }

            var demand = simulation.Population.ReplacementDemand(c) / parameters.Categories[c].Capacity;

            Assert.InRange(demand, 0.99, 1.01);
        }
    }

    // ---- the identity, byte for byte ----------------------------------------------------------

    /// <summary>
    /// **V5b.** A cycle table of ones reproduces the run without one, byte for byte.
    ///
    /// The mechanism is switched off by data rather than by a flag, exactly as the archetype table
    /// is: `Scale` snaps a share-weighted mean within a part in a billion to exactly 1, so `d = 1`
    /// comes back as `1.0` to the bit, `life_g · 1.0` is `life_g` to the bit, and every draw in the
    /// run happens in the same place in the same stream.
    /// </summary>
    [Fact]
    public void ACycleTableOfOnes_ReproducesTheRunWithoutOne()
    {
        var plain = Sized(
            Grouped() with { Run = Grouped().Run with { Replacement = Replacement.Hazard, Ticks = 24, WarmupTicks = 0 } },
            households: 800);

        var ones = string.Join(", ", plain.Categories.Select(c => Invariant($"{c.Name} = 1.0")));

        var withCycles = Load(
            Invariant($"[archetypes.average]\nshare = 1.0\nd = {{ {ones} }}\n"),
            plain);

        Assert.Equal(plain.Categories.Count, Type(withCycles, "average").D.Count);
        Assert.All(Type(withCycles, "average").D, d => Assert.Equal(1.0, d.Value));

        var expected = Series(plain, seed: 5);
        var actual = Series(withCycles, seed: 5);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// No age is drawn: 11-03's hazard is memoryless, and adding a cycle does not bring the calendar
    /// back.
    ///
    /// The sharper half of the claim is about the streams. Two runs whose *only* difference is the
    /// cycle table draw the same incomes, the same taste weights and the same archetypes for the
    /// same households — because nothing about `d` is drawn at all, so no stream moves.
    /// </summary>
    [Fact]
    public void NoAgeIsDrawn_AndTheCycleTableMovesNoStream()
    {
        var plain = Sized(
            Grouped() with { Run = Grouped().Run with { Replacement = Replacement.Hazard } },
            households: 500);

        var typed = Sized(Cycles(), households: 500);
        var goods = new GoodsTable(plain);

        var without = Households.Draw(plain, goods, runSeed: 6);
        var with = Households.Draw(typed, new GoodsTable(typed), runSeed: 6);

        Assert.Equal<IEnumerable<Money>>(without.Income, with.Income);
        Assert.Equal<IEnumerable<double>>(without.TasteWeight, with.TasteWeight);
        Assert.Equal<IEnumerable<double>>(without.Theta, with.Theta);
        Assert.Equal<IEnumerable<bool>>(without.IsAbstainer, with.IsAbstainer);

        // Every household opens owning a working unit of everything, and no age was initialised.
        Assert.All(with.Holds, held => Assert.True(held));
        Assert.All(with.Age, age => Assert.Equal(0, age));
    }

    /// <summary>
    /// The effective configuration of a cycle run reloads to itself.
    ///
    /// Both normalisations are idempotent — applied to a table that already satisfies the identity
    /// the scale is exactly 1 — which is what lets the loader print the *normalised* table and read
    /// it straight back. It is also why `d` is printed even where every entry is 1.0: a column of
    /// ones is the one column `deterministic` accepts, and leaving it out would make the printed
    /// file mean something different from the one that produced it.
    /// </summary>
    [Fact]
    public void ThePrintedCycleTable_ReloadsToItself()
    {
        var parameters = Town(5000);
        var reloaded = Load(parameters.ToToml(), SimulationParameters.Default);

        Assert.Equal(parameters.ToToml(), reloaded.ToToml());
        Assert.Equal(parameters.Archetypes, reloaded.Archetypes);

        foreach (var good in parameters.Categories)
        {
            Assert.Equal(1.0, reloaded.Archetypes.Types.Sum(t => t.Share / t.CycleFor(good.Name)), 12);
        }
    }

    /// <summary>A cycle run completes, with the conservation identity holding in every tick.</summary>
    [Fact]
    public void ACycleRunCompletes()
    {
        var parameters = Sized(
            Cycles() with { Run = Cycles().Run with { Ticks = 36, WarmupTicks = 0 } },
            households: 1200);

        var simulation = new Simulation(parameters, runSeed: 8);

        Assert.True(simulation.Run().IsSuccess);
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static SimulationParameters Load(string toml) => Load(toml, Grouped());

    private static SimulationParameters Load(string toml, SimulationParameters basis)
    {
        var loaded = ConfigurationLoader.FromToml(toml, basis);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        return loaded.Value;
    }

    /// <summary>A run's tick series, as text — the cheapest byte-for-byte comparison there is.</summary>
    private static string Series(SimulationParameters parameters, int seed)
    {
        var simulation = new Simulation(parameters, seed);

        Assert.True(simulation.Start().IsSuccess);

        var lines = new List<string>();

        for (var tick = 1; tick <= parameters.Run.Ticks; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);

            for (var c = 0; c < simulation.Goods.CategoryCount; c++)
            {
                for (var t = 0; t < simulation.Goods.TierCount; t++)
                {
                    lines.Add(Invariant(
                        $"{tick},{c},{t},{simulation.Market.Price(c, t).Cents},{simulation.Market.Sold(c, t)}"));
                }
            }
        }

        return string.Join("\n", lines);
    }

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

            // Everything replaced at once: this measures the demand the shelf faces, not what it
            // manages to supply.
            Array.Fill(population.Holds, true);
        }

        return counts;
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
}
