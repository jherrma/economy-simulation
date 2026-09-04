using System.Globalization;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// The archetype table, and the taste identity. See spec/stories/10-01.
///
/// The table is a *hypothesis about a population* and nothing in this repository can calibrate it.
/// What these tests check is the machinery around it: that the default is v1, that the identity
/// holds by construction rather than by the author's arithmetic, and that a steepness which would
/// turn the upgrade ladder upside down is refused against a bound read off the tier table.
/// </summary>
public sealed class ArchetypeTests
{
    private static readonly string[] Categories =
        [.. CategoryParameters.Default.Select(c => c.Name)];

    private static SimulationParameters Load(string toml)
    {
        var result = ConfigurationLoader.FromToml(toml);

        Assert.True(
            result.IsSuccess,
            "Expected this to load: " + string.Join("; ", result.Errors.Select(e => e.Message)));

        return result.Value;
    }

    private static IReadOnlyList<string> Problems(string toml)
    {
        var result = ConfigurationLoader.FromToml(toml);

        Assert.True(result.IsFailed, "Expected this to be rejected, and it loaded.");

        return [.. result.Errors.Select(e => e.Message)];
    }

    /// <summary>The committed `typed` population, as a scenario file on top of the defaults.</summary>
    private static SimulationParameters Typed()
    {
        var scenario = Scenario.FromFile(
            Path.Combine(Repo.Root, "config", "scenarios", "typed_credit_off.toml"));

        Assert.True(
            scenario.IsSuccess,
            "Expected the typed scenario to load: "
            + string.Join("; ", scenario.Errors.Select(e => e.Message)));

        return scenario.Value.Parameters;
    }

    // ---- the default is the identity table --------------------------------------------------

    /// <summary>
    /// The mechanism is switched off by **data**, not by a flag. That is what lets V5a be a
    /// byte-for-byte regression: there is no `archetypes_enabled` for the gate to be testing
    /// instead of the model.
    /// </summary>
    [Fact]
    public void AConfigurationNamingNoArchetype_GetsTheIdentityTable()
    {
        var loaded = Load("[run]\nseeds = 3\n");

        Assert.True(loaded.Archetypes.IsIdentity);
        Assert.Equal(SimulationParameters.Default.Archetypes, loaded.Archetypes);

        var only = Assert.Single(loaded.Archetypes.Types);

        Assert.Equal(Archetype.AverageName, only.Name);
        Assert.Equal(1.0, only.Share);
        Assert.All(Categories, c => Assert.Equal(1.0, only.WeightFor(c)));
        Assert.All(Categories, c => Assert.Equal(1.0, only.ExponentFor(c)));
    }

    [Fact]
    public void SigmaIdio_DefaultsToZero()
    {
        Assert.Equal(0.0, SimulationParameters.Default.Archetypes.SigmaIdio);
        Assert.Equal(0.0, Load(string.Empty).Archetypes.SigmaIdio);
        Assert.Equal(0.15, Load("[archetypes]\nsigma_idio = 0.15\n").Archetypes.SigmaIdio);
    }

    [Fact]
    public void ThereIsNoEnabledSwitch()
    {
        Assert.Contains(
            Problems("[archetypes]\narchetypes_enabled = true\n"),
            p => p.Contains("archetypes.archetypes_enabled", StringComparison.Ordinal));
    }

    // ---- absent versus unknown ----------------------------------------------------------------

    /// <summary>
    /// 02-02's asymmetry, inherited rather than re-implemented. A type that is average in leisure
    /// should not have to say so; a type that says `leasure` must not quietly be average in it too.
    /// </summary>
    [Fact]
    public void AnAbsentCategoryIsOne_AndAnUnknownOneIsAnError()
    {
        var loaded = Load(
            """
            [archetypes.only_food]
            share = 1.0
            w = { food = 1.0 }
            """);

        var type = Assert.Single(loaded.Archetypes.Types);

        Assert.All(Categories, c => Assert.Equal(1.0, type.WeightFor(c)));

        Assert.Contains(
            Problems(
                """
                [archetypes.typo]
                share = 1.0
                w = { leasure = 1.0 }
                """),
            p => p.Contains("archetypes.typo.w.leasure", StringComparison.Ordinal));

        Assert.Contains(
            Problems(
                """
                [archetypes.typo]
                share = 1.0
                kappa = { electronic = 1.0 }
                """),
            p => p.Contains("archetypes.typo.kappa.electronic", StringComparison.Ordinal));
    }

    // ---- the shares are a population -----------------------------------------------------------

    [Fact]
    public void SharesThatDoNotSumToOne_AreRejectedNamingTheSum()
    {
        var problems = Problems(
            """
            [archetypes.a]
            share = 0.3
            [archetypes.b]
            share = 0.4
            """);

        Assert.Contains(problems, p => p.Contains("archetypes.share", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("0.7", StringComparison.Ordinal));
    }

    [Fact]
    public void ATypeNobodyIs_IsRejected()
    {
        Assert.Contains(
            Problems(
                """
                [archetypes.nobody]
                share = 0.0
                [archetypes.everybody]
                share = 1.0
                """),
            p => p.Contains("archetypes.nobody.share", StringComparison.Ordinal));
    }

    // ---- the taste identity --------------------------------------------------------------------

    /// <summary>
    /// `Σ_A share_A · m_g,A = 1`, in every category, by construction. The table redistributes a
    /// category's demand across the population; it does not change how much of it there is, which
    /// is what keeps `v_g` meaning what §3.1 says it means.
    /// </summary>
    [Fact]
    public void TheTasteIdentityHoldsOnTheTypedTable()
    {
        AssertIdentity(Typed().Archetypes);
    }

    /// <summary>
    /// And on a table nobody designed. The identity is the loader's doing, so it cannot depend on
    /// the author having been careful.
    /// </summary>
    [Theory]
    [InlineData(20260904)]
    [InlineData(1)]
    [InlineData(77)]
    public void TheTasteIdentityHoldsOnARandomTable(int seed)
    {
        var random = new Random(seed);
        var shares = new[] { random.NextDouble() + 0.1, random.NextDouble() + 0.1, random.NextDouble() + 0.1 };
        var total = shares.Sum();

        var toml = string.Empty;

        for (var i = 0; i < shares.Length; i++)
        {
            var weights = Categories.Select(c =>
                string.Create(CultureInfo.InvariantCulture, $"{c} = {0.4 + (random.NextDouble() * 1.4):0.####}"));

            toml += string.Create(
                CultureInfo.InvariantCulture,
                $"[archetypes.t{i}]\nshare = {shares[i] / total:0.############}\nw = {{ {string.Join(", ", weights)} }}\n");
        }

        AssertIdentity(Load(toml).Archetypes);
    }

    /// <summary>The lines of one TOML section, up to the next heading.</summary>
    private static IReadOnlyList<string> Section(string toml, string heading)
    {
        var lines = toml.ReplaceLineEndings("\n").Split('\n');
        var start = Array.FindIndex(lines, l => string.Equals(l, heading, StringComparison.Ordinal));

        Assert.True(start >= 0, $"No {heading} in the effective configuration.");

        return
        [
            .. lines
                .Skip(start + 1)
                .TakeWhile(l => !l.StartsWith('[')),
        ];
    }

    private static void AssertIdentity(ArchetypeParameters archetypes)
    {
        foreach (var category in Categories)
        {
            var mean = archetypes.Types.Sum(a => a.Share * a.WeightFor(category));

            Assert.Equal(1.0, mean, 9);
        }
    }

    /// <summary>
    /// The authored weights do not satisfy the identity and are not meant to; the loader divides
    /// each column by its share-weighted mean. §3.5's column scales are 1.0150 for food and 1.0300
    /// for electronics, so `prudent`'s 0.90 lands at 0.8867 and `gadget`'s 1.60 at 1.5534.
    /// </summary>
    [Fact]
    public void TheAuthoredWeightsAreRelative_AndTheLoaderDoesTheDivision()
    {
        var types = Typed().Archetypes.Types.ToDictionary(a => a.Name, StringComparer.Ordinal);

        Assert.Equal(0.90 / 1.015, types["prudent"].WeightFor("food"), 12);
        Assert.Equal(1.60 / 1.030, types["gadget"].WeightFor("electronics"), 12);
        Assert.Equal(1.55 / 1.000, types["gadget"].WeightFor("hobby"), 12);

        // family_practical omits leisure from `w`, so it is average in it — 1.00 / 0.97.
        Assert.Equal(1.00 / 0.970, types["family_practical"].WeightFor("leisure"), 12);
    }

    /// <summary>
    /// What the author wrote and what the model ran are different numbers, and it is the second
    /// that has to reach the reader — the campaign manifest hashes the effective configuration.
    /// </summary>
    [Fact]
    public void TheEffectiveConfigurationCarriesTheNormalisedWeights()
    {
        var toml = Typed().ToToml();
        var prudent = Section(toml, "[archetypes.prudent]");
        var weights = Assert.Single(prudent, l => l.StartsWith("w = ", StringComparison.Ordinal));

        // 0.90 is what the scenario file says; 0.90 / 1.015 is what the model ran.
        Assert.Contains("food = 0.88669950738916", weights, StringComparison.Ordinal);
        Assert.DoesNotContain("food = 0.9,", weights, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it reloads to itself. A run rebuilt from its own effective configuration has to be the
    /// same run, or normalising a table that is already normalised would quietly move it — in
    /// exactly the comparison V5a exists to make.
    /// </summary>
    [Fact]
    public void TheNormalisedTableReloadsToItself()
    {
        var typed = Typed();
        var reloaded = Load(typed.ToToml());

        Assert.Equal(typed.Archetypes, reloaded.Archetypes);
        Assert.Equal(typed.ToToml(), reloaded.ToToml());
    }

    // ---- the committed table is the specification's table ------------------------------------------

    /// <summary>
    /// The scenario file against §3.5, weight by weight.
    ///
    /// This is the same rule as SchemaTests': a parameter not in `02-PARAMETERS.md` does not exist,
    /// and the way that rule fails is that a number gets tuned in a config file and the
    /// specification quietly stops describing the population that produced the results. The
    /// specification's *authored* column is read here and normalised by this test, so the committed
    /// file and the loader's arithmetic are checked against the document rather than against each
    /// other.
    /// </summary>
    [Fact]
    public void TheTypedScenario_IsTheSpecificationsTable()
    {
        var spec = SpecFile.Tables("#### The `typed` table")[0];
        var categories = spec[0].Skip(2).Select(c => SpecFile.Clean(c).ToLowerInvariant()).ToArray();

        Assert.Equal(Categories.Order(StringComparer.Ordinal), categories.Order(StringComparer.Ordinal));

        var authored = new List<(string Name, double Share, double[] W, double[] Kappa)>();

        for (var row = 1; row < spec.Count; row += 2)
        {
            var name = SpecFile.Clean(spec[row][0]);

            Assert.EndsWith(" w", name, StringComparison.Ordinal);
            Assert.Equal("kappa", SpecFile.Clean(spec[row + 1][0]));

            authored.Add((
                name[..^2],
                SpecFile.Number(spec[row][1]),
                [.. spec[row].Skip(2).Select(SpecFile.Number)],
                [.. spec[row + 1].Skip(2).Select(SpecFile.Number)]));
        }

        var loaded = Typed().Archetypes.Types.ToDictionary(a => a.Name, StringComparer.Ordinal);

        Assert.Equal(authored.Count, loaded.Count);

        for (var c = 0; c < categories.Length; c++)
        {
            var scale = authored.Sum(a => a.Share * a.W[c]);

            foreach (var (name, _, w, kappa) in authored)
            {
                Assert.Equal(w[c] / scale, loaded[name].WeightFor(categories[c]), 12);
                Assert.Equal(kappa[c], loaded[name].ExponentFor(categories[c]), 12);
            }
        }

        foreach (var (name, share, _, _) in authored)
        {
            Assert.Equal(share, loaded[name].Share);
        }
    }

    /// <summary>
    /// And the normalised table §3.5 publishes is the one the loader produces — a check on the
    /// document's own arithmetic, which nothing else in the project would notice going wrong.
    /// </summary>
    [Fact]
    public void ThePublishedNormalisedTable_IsWhatTheLoaderProduces()
    {
        var spec = SpecFile.Tables("#### The taste identity, and what the loader does with it")[0];
        var categories = spec[0].Skip(1).Select(c => SpecFile.Clean(c).ToLowerInvariant()).ToArray();
        var loaded = Typed().Archetypes.Types.ToDictionary(a => a.Name, StringComparer.Ordinal);

        foreach (var row in spec.Skip(1))
        {
            var name = SpecFile.Clean(row[0]);

            if (!loaded.TryGetValue(name, out var type))
            {
                // The last row is the share-weighted mean, which is the identity itself.
                Assert.Equal("share-weighted mean", name);
                Assert.All(row.Skip(1), cell => Assert.Equal(1.0, SpecFile.Number(cell)));
                continue;
            }

            for (var c = 0; c < categories.Length; c++)
            {
                Assert.Equal(SpecFile.Number(row[c + 1]), type.WeightFor(categories[c]), 4);
            }
        }
    }

    // ---- kappa is not normalised ----------------------------------------------------------------

    /// <summary>
    /// `kappa` is an absolute statement about how much a type cares about quality. A table whose
    /// population mean is below 1 is a genuinely more budget-minded town rather than a mis-scaled
    /// one — which is *why* such a table needs its own `credit_off` arm, and why the sweep grid
    /// carries a `typed_kappa_neutral` control.
    /// </summary>
    [Fact]
    public void ATableWhoseMeanKappaIsNotOne_LoadsUnchanged()
    {
        var loaded = Load(
            """
            [archetypes.a]
            share = 0.5
            kappa = { food = 0.80 }
            [archetypes.b]
            share = 0.5
            kappa = { food = 0.90 }
            """);

        var types = loaded.Archetypes.Types.ToDictionary(a => a.Name, StringComparer.Ordinal);

        Assert.Equal(0.80, types["a"].ExponentFor("food"));
        Assert.Equal(0.90, types["b"].ExponentFor("food"));
        Assert.Equal(0.85, loaded.Archetypes.Types.Sum(a => a.Share * a.ExponentFor("food")), 12);
    }

    /// <summary>§3.5: the population-mean kappa of the typed table is below 1 in every category.</summary>
    [Fact]
    public void TheTypedTablesMeanKappa_IsBelowOneEverywhere()
    {
        var archetypes = Typed().Archetypes;

        foreach (var category in Categories)
        {
            var mean = archetypes.Types.Sum(a => a.Share * a.ExponentFor(category));

            Assert.InRange(mean, 0.90, 0.9999);
        }
    }

    // ---- the bound on kappa ----------------------------------------------------------------------

    /// <summary>
    /// The bound is `ln(price_mult_budget) / ln(value_mult_budget)`, which at the v1 tiers is
    /// 1.3245. Asserted against the algebra rather than against the number, so that the bisection
    /// and §5.4 cannot drift apart.
    /// </summary>
    [Fact]
    public void TheBound_IsWhereTheBudgetStepStopsBeatingTheUpgrade()
    {
        var tiers = TierParameters.Default;
        var expected = Math.Log(tiers[0].PriceMult) / Math.Log(tiers[0].ValueMult);

        Assert.Equal(expected, QualityLadder.MaxExponent(tiers), 6);
        Assert.Equal(1.3245, QualityLadder.MaxExponent(tiers), 4);

        Assert.True(QualityLadder.IsOrdered(tiers, expected - 1e-6));
        Assert.False(QualityLadder.IsOrdered(tiers, expected + 1e-6));
    }

    [Fact]
    public void AKappaAtOrAboveTheBound_IsRejected()
    {
        var problems = Problems(
            """
            [archetypes.steep]
            share = 1.0
            kappa = { food = 1.5 }
            """);

        Assert.Contains(problems, p => p.Contains("archetypes.steep.kappa.food", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("1.3245", StringComparison.Ordinal));

        Assert.Contains(
            Problems(
                """
                [archetypes.flat]
                share = 1.0
                kappa = { food = 0.0 }
                """),
            p => p.Contains("archetypes.flat.kappa.food", StringComparison.Ordinal));
    }

    /// <summary>
    /// The bound is computed from the tier table, never written down. A literal would be correct
    /// today and silently wrong the first time somebody edits a tier multiplier — which is the class
    /// of error V6's monotonicity assertion exists to catch, arriving from the one direction V6
    /// cannot see, namely configuration.
    ///
    /// And a literal would have been wrong in a second way. `ln(price_mult_budget) /
    /// ln(value_mult_budget)` is the bound at the *v1* tiers because the first ordering condition
    /// is the one that binds there. Make the budget tier better value — 0.68 to 0.75 — and that
    /// condition relaxes to 1.7757 while the second one, "upgrading to standard must still beat
    /// upgrading to premium", takes over at 1.7073. Which condition binds is itself a property of
    /// the tier table, so the bound is found rather than derived.
    /// </summary>
    [Fact]
    public void TheAcceptedRangeFollowsAChangedTierMultiplier()
    {
        const string flatter = "[tiers.budget]\nvalue_mult = 0.75\n";

        var moved = Load(flatter + "[archetypes.steep]\nshare = 1.0\nkappa = { food = 1.5 }\n");
        var bound = QualityLadder.MaxExponent(moved.Tiers);

        Assert.True(bound > QualityLadder.MaxExponent(TierParameters.Default));
        Assert.True(QualityLadder.IsOrdered(moved.Tiers, bound - 1e-6));
        Assert.False(QualityLadder.IsOrdered(moved.Tiers, bound + 1e-6));

        // The first condition would have allowed 1.7757; the second is what actually binds.
        Assert.True(bound < Math.Log(0.60) / Math.Log(0.75));
        Assert.Equal(1.7073, bound, 4);

        Assert.Equal(1.5, moved.Archetypes.Types[0].ExponentFor("food"));

        // And the same steepness under the unchanged tiers is refused, so the difference really is
        // the tier table and not the exponent.
        Assert.Contains(
            Problems("[archetypes.steep]\nshare = 1.0\nkappa = { food = 1.5 }\n"),
            p => p.Contains("archetypes.steep.kappa.food", StringComparison.Ordinal));
    }

    // ---- a scenario can carry a table ---------------------------------------------------------------

    [Fact]
    public void TheTypedScenario_ChangesNothingButTheTable()
    {
        var scenario = Scenario.FromFile(
            Path.Combine(Repo.Root, "config", "scenarios", "typed_credit_off.toml"));

        Assert.True(scenario.IsSuccess);

        Assert.All(
            scenario.Value.Changes,
            key => Assert.StartsWith("archetypes.", key, StringComparison.Ordinal));

        Assert.Equal(4, scenario.Value.Parameters.Archetypes.Types.Count);
        Assert.False(scenario.Value.Parameters.Credit.CreditEnabled);
    }

    /// <summary>
    /// The order of the types is the order of the *names*, not the order somebody typed the
    /// sections in. Two files stating the same population have to be the same population — under
    /// 10-02 the order decides which household is assigned to which type.
    /// </summary>
    [Fact]
    public void TheTableIsOrderedByName_NotByTheFile()
    {
        var written = Load(
            """
            [archetypes.zebra]
            share = 0.5
            [archetypes.alpha]
            share = 0.5
            """);

        Assert.Equal(["alpha", "zebra"], written.Archetypes.Types.Select(a => a.Name));
    }
}
