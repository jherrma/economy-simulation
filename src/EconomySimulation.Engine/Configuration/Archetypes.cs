namespace EconomySimulation.Engine.Configuration;

/// <summary>
/// One category's entry in an archetype's row — its `w` or its `kappa`.
///
/// A list of pairs rather than a dictionary, because a configuration is compared for equality and
/// printed back out, and both want a fixed order. The loader sorts by category name, so two files
/// that state the same weights in a different order are the same configuration.
/// </summary>
/// <param name="Category">The category this number applies to.</param>
/// <param name="Value">The weight, or the exponent.</param>
public readonly record struct CategoryWeight(string Category, double Value);

/// <summary>
/// One household type (`spec/02-PARAMETERS.md` §3.5): a population share, a per-category taste
/// level, and a per-category quality steepness.
///
/// The two numbers are deliberately separate. In v1 they were the same number — a household with a
/// high `w_h` wanted more of everything *and* climbed higher up every tier ladder — so "buys the
/// best food, but only one portion of it" was not something the model could say. `w` is the level
/// and `kappa` is the steepness, and a type is the pair of them read together.
/// </summary>
public sealed record Archetype
{
    /// <summary>The single type of the identity table. The default population is one of these.</summary>
    public const string AverageName = "average";

    public required string Name { get; init; }

    /// <summary>Population share. The shares over a table sum to 1.</summary>
    public required double Share { get; init; }

    /// <summary>
    /// The **score multiplier** `m`, per **category label** — how much better or worse than average
    /// a candidate scores for this type. This is what §3.5's table states and it is *not* the taste
    /// weight: see <see cref="TasteWeightFor"/>.
    ///
    /// Keyed by the label rather than by the goods-table row, because a type is a statement about
    /// wanting electronics, not about wanting a laptop more than a phone (§3.6). Under §3.1 a label
    /// *is* a row and the distinction is invisible, which is why every file written before E11
    /// still means what it meant.
    ///
    /// What the loader stores here is the **normalised** multiplier: the share-weighted mean over a
    /// table is exactly 1 in every category, so a table redistributes a category's demand across
    /// the population without changing how much of it there is.
    /// </summary>
    public required IReadOnlyList<CategoryWeight> W { get; init; }

    /// <summary>
    /// The quality steepness, applied as `value_mult(tier)^kappa`. **Not** normalised: it is an
    /// absolute statement about how much a type cares about quality, and a table whose population
    /// mean is below 1 is a genuinely more budget-minded town rather than a mis-scaled one. That
    /// is also why such a table has to be compared against its own `credit_off` arm.
    /// </summary>
    public required IReadOnlyList<CategoryWeight> Kappa { get; init; }

    /// <summary>
    /// The **replacement-cycle multiplier** `d`, per **good** (§3.7): the good's life times this is
    /// the life this type actually gets out of it. Below 1 replaces sooner, above 1 keeps it longer.
    ///
    /// Keyed by the row and not by the label, deliberately the other way round from <see cref="W"/>.
    /// A cycle is a fact about a particular product — `gadget` churns its phone and leaves its
    /// television alone — where taste is a fact about a category, and §3.7's table has three
    /// different multipliers inside electronics to prove it.
    ///
    /// Stored **harmonically normalised** — `Σ_A share_A / d[A][g] = 1` — because demand per tick is
    /// `1 / life`, so it is the reciprocal that has to average to one for `capacity` to stay sized
    /// right. See <see cref="ConfigurationLoader"/>'s Normalise for why the arithmetic mean is the
    /// expensive mistake here.
    ///
    /// Meaningful only under `replacement = "hazard"`, and only on a good with a life to stretch;
    /// the loader rejects a `d` anywhere else rather than rounding it away.
    /// </summary>
    public required IReadOnlyList<CategoryWeight> D { get; init; }

    /// <summary>This type's score multiplier for a category label. **An absent label means 1.0.**</summary>
    public double WeightFor(string label) => Lookup(W, label);

    /// <summary>This type's quality steepness for a category label. An absent label means 1.0.</summary>
    public double ExponentFor(string label) => Lookup(Kappa, label);

    /// <summary>This type's replacement-cycle multiplier for a good. An absent good means 1.0.</summary>
    public double CycleFor(string good) => Lookup(D, good);

    /// <summary>
    /// `ŵ = m / d` — the taste weight the decision actually uses, **derived and never authored**.
    ///
    /// `flow_cost` divides by the household's own life (`01-SIMULATION.md` §5.5), so a score is
    /// proportional to `ŵ · d` and the two tables compose to `m`. Author `ŵ` directly instead and
    /// they fight: a type that replaces its phone 48% more often has already divided away almost
    /// all of the 1.55 multiplier meant to make it the top phone bidder, and the type that keeps
    /// its phone longest comes out bidding highest — the opposite of the table's intent.
    ///
    /// The consequence is that this can be much larger than any number in the file, and that is
    /// correct rather than alarming. `gadget`'s phone comes out at 2.299: a household that both
    /// churns and buys well values a phone-month at more than twice the average, because those are
    /// competing claims on one budget.
    /// </summary>
    public double TasteWeightFor(string label, string good) => WeightFor(label) / CycleFor(good);

    /// <summary>
    /// One type, average in everything — v1's population, written as a table.
    ///
    /// Two lists because the two tables are keyed differently: taste by label, cycle by good. Under
    /// §3.1 they are the same six strings.
    /// </summary>
    public static Archetype Identity(IEnumerable<string> labels, IEnumerable<string> goods)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(goods);

        var ones = Ones(labels);

        return new Archetype { Name = AverageName, Share = 1.0, W = ones, Kappa = ones, D = Ones(goods) };
    }

    /// <summary>Every named key at 1.0, in name order.</summary>
    internal static IReadOnlyList<CategoryWeight> Ones(IEnumerable<string> names) =>
        [.. names.OrderBy(c => c, StringComparer.Ordinal).Select(c => new CategoryWeight(c, 1.0))];

    /// <summary>Two types are equal when they say the same thing, not when they share a list.</summary>
    public bool Equals(Archetype? other) =>
        other is not null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && Share.Equals(other.Share)
        && W.SequenceEqual(other.W)
        && Kappa.SequenceEqual(other.Kappa)
        && D.SequenceEqual(other.D);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Share);

        foreach (var weight in W)
        {
            hash.Add(weight);
        }

        foreach (var exponent in Kappa)
        {
            hash.Add(exponent);
        }

        foreach (var cycle in D)
        {
            hash.Add(cycle);
        }

        return hash.ToHashCode();
    }

    private static double Lookup(IReadOnlyList<CategoryWeight> weights, string category)
    {
        for (var i = 0; i < weights.Count; i++)
        {
            if (string.Equals(weights[i].Category, category, StringComparison.Ordinal))
            {
                return weights[i].Value;
            }
        }

        // Absent means 1.0 — a type that is average in leisure should not have to say so, and the
        // file then reads as a statement of what makes the type different. A *misspelt* category
        // is a different thing entirely and the loader rejects it before anything gets here.
        return 1.0;
    }
}

/// <summary>
/// §3.5 as a section: the table of types, and the spread of the per-household residual.
///
/// **The default is the identity table** — one type, share 1.0, every `w` and every `kappa` at 1.0
/// — so v1 is what a configuration naming no archetype receives, and there is no
/// `archetypes_enabled` boolean anywhere. The mechanism is switched off by data, which is what
/// lets V5a be a byte-for-byte regression rather than a test of a flag.
/// </summary>
public sealed record ArchetypeParameters
{
    /// <summary>
    /// σ of the per-household, per-category residual `ε_h,g`, mean exactly 1.
    ///
    /// Zero by default, so taste is perfectly correlated across categories exactly as in v1.
    /// Raising it walks that correlation toward zero, which is the sweep for "does it matter that
    /// the same households want everything".
    /// </summary>
    public double SigmaIdio { get; init; }

    /// <summary>
    /// Whether to divide each `kappa` column by its share-weighted mean, as `w` is always divided.
    ///
    /// False everywhere except the `typed_kappa_neutral` control (`02-PARAMETERS.md` §3.5), which is
    /// *defined* as that operation. `kappa` is unnormalised by design — a table whose population
    /// mean is below 1 is a genuinely more budget-minded town — and that is exactly why the control
    /// is needed: without it a typed-versus-untyped comparison confounds "taste is heterogeneous"
    /// with "taste is cheaper". Naming the operation keeps the control one line away from `typed`
    /// instead of twenty-four numbers somebody worked out by hand and nothing re-checks.
    /// </summary>
    public bool NormaliseKappa { get; init; }

    public IReadOnlyList<Archetype> Types { get; init; } =
    [
        Archetype.Identity(
            CategoryParameters.Default.Select(c => c.Label),
            CategoryParameters.Default.Select(c => c.Name)),
    ];

    public bool Equals(ArchetypeParameters? other) =>
        other is not null
        && SigmaIdio.Equals(other.SigmaIdio)
        && NormaliseKappa == other.NormaliseKappa
        && Types.SequenceEqual(other.Types);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(SigmaIdio);
        hash.Add(NormaliseKappa);

        foreach (var type in Types)
        {
            hash.Add(type);
        }

        return hash.ToHashCode();
    }

    /// <summary>Whether this is v1's population: one type, average in everything.</summary>
    public bool IsIdentity =>
        SigmaIdio == 0.0
        && Types.Count == 1
        && Types[0].Share == 1.0
        && Types[0].W.All(w => w.Value == 1.0)
        && Types[0].Kappa.All(k => k.Value == 1.0)
        && Types[0].D.All(d => d.Value == 1.0);
}

/// <summary>
/// What the tier table implies about how steep a quality exponent may be.
///
/// `kappa` raises every tier's value multiplier to a power, and past a point that inverts the
/// upgrade ladder: `value_mult_budget^kappa` falls below `price_mult_budget`, "buy the budget unit"
/// scores below "upgrade it to standard", and the walk's stop rule stops meaning what it says.
/// At the v1 tiers the first ordering condition binds at `ln(0.60)/ln(0.68) = 1.3245`.
///
/// The bound is **computed from the tier table**, never written down. A literal would be correct
/// today and silently wrong the first time somebody edits a tier multiplier — which is exactly the
/// class of error V6's monotonicity assertion exists to catch, arriving from the one direction V6
/// cannot see, namely configuration.
///
/// Two things it does not promise, and both matter. It is a bound at **opening** prices: the
/// condition that actually has to hold is `price_budget / price_standard ≤ value_mult_budget^kappa`,
/// so from the opening 0.60 the budget shelf need only become 13.3% dearer relative to standard at
/// `kappa = 1`, and 2.9% at `kappa = 1.25`, for the ladder to invert — a move §10.3 has observed
/// several times over. And it is a bound on the *table*, not on the economy: nothing here says a
/// given `kappa` is a good description of anybody.
/// </summary>
public static class QualityLadder
{
    /// <summary>
    /// The value-for-money of each step up the ladder — `Δvalue / Δprice`, with the tier's value
    /// multiplier raised to <paramref name="kappa"/>. `NaN` where a step costs nothing, which the
    /// ladder check reports on its own account.
    ///
    /// One definition of the ratio, two readers: the loader's diminishing-returns check walks it at
    /// `kappa = 1` to say *which* step went wrong, and <see cref="MaxExponent"/> walks it at many
    /// exponents to find where ordering breaks. A second copy of this arithmetic is a second place
    /// for the ladder to mean something different.
    /// </summary>
    public static double[] StepRatios(IReadOnlyList<TierParameters> tiers, double kappa)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        var ratios = new double[tiers.Count];

        // The step onto the bottom tier is an increment on holding nothing, so both previous
        // quantities start at zero — the same telescoping the candidate ladder uses.
        var previousValue = 0.0;
        var previousPrice = 0.0;

        for (var t = 0; t < tiers.Count; t++)
        {
            var value = Math.Pow(tiers[t].ValueMult, kappa);
            var price = tiers[t].PriceMult;
            var deltaPrice = price - previousPrice;

            ratios[t] = deltaPrice > 0 ? (value - previousValue) / deltaPrice : double.NaN;

            previousValue = value;
            previousPrice = price;
        }

        return ratios;
    }

    /// <summary>Whether every step up the ladder is worse value for money than the step below it.</summary>
    public static bool IsOrdered(IReadOnlyList<TierParameters> tiers, double kappa)
    {
        var previous = double.PositiveInfinity;

        foreach (var ratio in StepRatios(tiers, kappa))
        {
            // NaN fails this, which is what we want: a step that costs nothing is not a ladder.
            if (!(ratio < previous))
            {
                return false;
            }

            previous = ratio;
        }

        return true;
    }

    /// <summary>
    /// The largest quality exponent this tier table admits: the smallest `kappa ≥ 1` at which the
    /// opening ladder stops being ordered, found by bisection to within a part in a billion.
    ///
    /// `NaN` when the table is not even ordered at `kappa = 1`, because then the tier table itself
    /// is broken and the loader is already reporting it; a bound derived from a broken ladder would
    /// be a second, confusing complaint about the same mistake.
    /// </summary>
    public static double MaxExponent(IReadOnlyList<TierParameters> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        if (!IsOrdered(tiers, 1.0))
        {
            return double.NaN;
        }

        var low = 1.0;
        var high = 2.0;

        // Doubling rather than a fixed upper bound: the tier table is a parameter, and a table with
        // a very flat value ladder can admit a very steep exponent.
        while (IsOrdered(tiers, high))
        {
            if (high >= 1e6)
            {
                return double.PositiveInfinity;
            }

            low = high;
            high *= 2.0;
        }

        for (var i = 0; i < 64 && high - low > 1e-9; i++)
        {
            var middle = (low + high) / 2.0;

            if (IsOrdered(tiers, middle))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
