using System.Globalization;
using System.Text;

namespace EconomySimulation.Engine.Configuration;

/// <summary>
/// Every parameter of the v1 model, once, with the default from `spec/02-PARAMETERS.md`.
///
/// A parameter not in that file does not exist. The rule matters because the full model's appendix
/// grew to seventeen parameters with no empirical anchor, largely because a number could be
/// introduced at a call site and documented later, or not at all — and SchemaTests compares every
/// default here against the specification file, so the two cannot drift apart quietly.
///
/// Every parameter that gates a behaviour defaults to the setting that **disables** it. That is
/// what makes the baseline free: `credit_off` is not a scenario anyone has to configure, it is
/// what running the defaults gives you, so the control arm cannot drift away from the treatment
/// arm's setup.
/// </summary>
public sealed record SimulationParameters
{
    public RunParameters Run { get; init; } = new();

    public IncomeParameters Income { get; init; } = new();

    public IReadOnlyList<CategoryParameters> Categories { get; init; } = CategoryParameters.Default;

    public IReadOnlyList<TierParameters> Tiers { get; init; } = TierParameters.Default;

    /// <summary>
    /// §3.5 — the population's types. The default is the identity table, which is v1 exactly, so
    /// the default configuration is still the baseline and the byte-for-byte rule needs no switch.
    /// </summary>
    public ArchetypeParameters Archetypes { get; init; } = new();

    public DecisionParameters Decision { get; init; } = new();

    public CreditParameters Credit { get; init; } = new();

    public PriceParameters Prices { get; init; } = new();

    public MoneyParameters Money { get; init; } = new();

    /// <summary>The specification's defaults, in full.</summary>
    public static SimulationParameters Default { get; } = new();

    /// <summary>
    /// Two configurations are equal when they name the same parameters, not when they happen to
    /// share a list object. A record's generated equality compares <see cref="IReadOnlyList{T}"/>
    /// by reference, which would make a loaded configuration unequal to the defaults it was
    /// loaded from — and every test that compares two configurations would then pass or fail for
    /// reasons that have nothing to do with the parameters.
    /// </summary>
    public bool Equals(SimulationParameters? other) =>
        other is not null
        && Run == other.Run
        && Income == other.Income
        && Categories.SequenceEqual(other.Categories)
        && Tiers.SequenceEqual(other.Tiers)
        && Archetypes == other.Archetypes
        && Decision == other.Decision
        && Credit == other.Credit
        && Prices == other.Prices
        && Money == other.Money;

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Run);
        hash.Add(Income);
        foreach (var category in Categories)
        {
            hash.Add(category);
        }

        foreach (var tier in Tiers)
        {
            hash.Add(tier);
        }

        hash.Add(Archetypes);
        hash.Add(Decision);
        hash.Add(Credit);
        hash.Add(Prices);
        hash.Add(Money);

        return hash.ToHashCode();
    }

    /// <summary>
    /// The same configuration for a different number of households, with every capacity recomputed.
    ///
    /// `capacity` is `round(households / life)` by definition (`spec/02-PARAMETERS.md` §3.1) — the
    /// steady-state replacement demand, and the only non-arbitrary way to size it. Changing the
    /// population without it is not a smaller town: it is a town whose shelves were stocked for a
    /// different one, and the symptom is not obvious. At a fifth of the households and the same
    /// shelves, supply is five times demand, every price falls to the floor, households cannot
    /// spend what they earn, and the pool drains until the run halts on its own calibration check
    /// around tick 60 — which reads as a result about the parameters rather than as the mistake it
    /// is. The loader already refuses such a file; this is the same rule for a configuration built
    /// in code, which the gates and the tests both need.
    /// </summary>
    public SimulationParameters WithHouseholds(int households)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(households, 1);

        return this with
        {
            Run = Run with { Households = households },
            Categories = [.. Categories.Select(c => c with { Capacity = DerivedCapacity(households, c.Life) })],
        };
    }

    /// <summary>`round(households / life)`, the steady-state replacement demand in units.</summary>
    public static int DerivedCapacity(int households, int life) =>
        life < 1 ? 0 : (int)Math.Round((double)households / life, MidpointRounding.AwayFromZero);

    // ---- derived money quantities (§7) ---------------------------------------------------

    /// <summary>`households × mean_income × opening_cash_share` — €650,000 at the defaults.</summary>
    public Money OpeningHouseholdCash =>
        Income.MeanIncome.Scaled(Income.OpeningCashShare) * Run.Households;

    /// <summary>
    /// Twenty-four months of total income. The pool has no behaviour and its size is not
    /// economically meaningful; it is a buffer that makes the warm-up transient survivable, and
    /// then the residual drain of §V4 survivable for six hundred ticks. One month would halt the
    /// run around tick 11, and twelve around tick 700.
    /// </summary>
    public Money OpeningPool =>
        Income.MeanIncome * Run.Households * Money.OpeningPoolMonths;

    /// <summary>
    /// M0 — €16,250,000 at the defaults. Fixed for the run; only lending and repayment change the
    /// money stock, and V1 is the assertion that says so every tick.
    /// </summary>
    public Money M0 => OpeningHouseholdCash + OpeningPool;

    /// <summary>
    /// The whole configuration as TOML, so a run's parameters can be recovered without reading
    /// the code or trusting that the defaults have not changed since. This is the *resolved*
    /// configuration — what the run actually used, not what the input file happened to say.
    /// </summary>
    public string ToToml()
    {
        var toml = new StringBuilder();

        toml.AppendLine("# The effective configuration of this run: the input file with every");
        toml.AppendLine("# absent key filled from spec/02-PARAMETERS.md. Defaults change; a CSV");
        toml.AppendLine("# whose parameters cannot be recovered is not a result.");
        toml.AppendLine();

        toml.AppendLine("[run]");
        Write(toml, "households", Run.Households);
        Write(toml, "abstainer_share", Run.AbstainerShare);
        Write(toml, "ticks", Run.Ticks);
        Write(toml, "warmup_ticks", Run.WarmupTicks);
        Write(toml, "seeds", Run.Seeds);
        Write(toml, "scenario", Run.Scenario);
        Write(toml, "replacement", Run.Replacement.ToTomlValue());
        toml.AppendLine();

        toml.AppendLine("[income]");
        Write(toml, "mean_income", Income.MeanIncome);
        Write(toml, "sigma_income", Income.SigmaIncome);
        Write(toml, "opening_cash_share", Income.OpeningCashShare);
        toml.AppendLine();

        // Always `replace = true`, and never mind that the rows below happen to be the defaults:
        // an effective configuration is the table that ran, and a file that has to be overlaid on
        // the right basis to mean what it says is not that. This is what lets a run be rebuilt from
        // its own output six months from now without knowing which defaults it was written against.
        toml.AppendLine("[categories]");
        Write(toml, "replace", true);
        toml.AppendLine();

        foreach (var category in Categories)
        {
            toml.AppendLine(CultureInfo.InvariantCulture, $"[categories.{category.Name}]");
            Write(toml, "category", category.Label);
            Write(toml, "life", category.Life);
            Write(toml, "capacity", category.Capacity);
            Write(toml, "price_ref", category.PriceRef);

            // Round-trip precision on both, and only on these two, for the reason the archetype
            // weights get it: `v` is *derived* from `base_score` wherever one is authored, and a
            // configuration rebuilt from a four-decimal print of a derived number is refused by the
            // very check that makes the derivation worth having. Everything else in this file was
            // typed by a person and is printed the way a person would read it.
            if (category.BaseScore > 0.0)
            {
                WriteExactly(toml, "base_score", category.BaseScore);
                WriteExactly(toml, "v", category.V);
            }
            else
            {
                Write(toml, "v", category.V);
            }

            Write(toml, "necessity", category.Necessity);
            Write(toml, "financeable", category.Financeable);
            Write(toml, "term", category.Term);
            toml.AppendLine();
        }

        foreach (var tier in Tiers)
        {
            toml.AppendLine(CultureInfo.InvariantCulture, $"[tiers.{tier.Name}]");
            Write(toml, "price_mult", tier.PriceMult);
            Write(toml, "value_mult", tier.ValueMult);
            Write(toml, "unit_share", tier.UnitShare);
            toml.AppendLine();
        }

        toml.AppendLine("[archetypes]");
        Write(toml, "sigma_idio", Archetypes.SigmaIdio);
        Write(toml, "normalise_kappa", Archetypes.NormaliseKappa);
        toml.AppendLine();

        // The **normalised** weights, not the authored ones: the campaign manifest hashes this
        // file, and what a reader six months from now needs is the table that ran.
        foreach (var type in Archetypes.Types)
        {
            toml.AppendLine(CultureInfo.InvariantCulture, $"[archetypes.{type.Name}]");
            Write(toml, "share", type.Share);
            WriteWeights(toml, "w", type.W);
            WriteWeights(toml, "kappa", type.Kappa);

            // Printed even where every entry is 1.0, and printed *normalised*, so that the effective
            // configuration reloads to itself: both normalisations are idempotent, and a `d` column
            // of ones is the one column `deterministic` accepts.
            WriteWeights(toml, "d", type.D);
            toml.AppendLine();
        }

        toml.AppendLine("[decision]");
        Write(toml, "lambda", Decision.Lambda);
        Write(toml, "sigma_w", Decision.SigmaW);
        Write(toml, "subsistence_share", Decision.SubsistenceShare);
        Write(toml, "buffer_months", Decision.BufferMonths);
        Write(toml, "affordability_horizon", Decision.AffordabilityHorizon.ToTomlValue());
        toml.AppendLine();

        toml.AppendLine("[credit]");
        Write(toml, "credit_enabled", Credit.CreditEnabled);
        Write(toml, "loan_rate", Credit.LoanRate.PercentPerAnnum);
        Write(toml, "money_creation", Credit.MoneyCreation);
        Write(toml, "theta_min", Credit.ThetaMin);
        Write(toml, "theta_max", Credit.ThetaMax);
        toml.AppendLine();

        toml.AppendLine("[prices]");
        Write(toml, "k", Prices.K);
        Write(toml, "price_floor", Prices.PriceFloor);
        Write(toml, "rationing", Prices.Rationing.ToTomlValue());
        toml.AppendLine();

        toml.AppendLine("[money]");
        Write(toml, "opening_pool_months", Money.OpeningPoolMonths);

        return toml.ToString();
    }

    /// <summary>
    /// The fully-qualified keys whose **effective** value differs between two configurations.
    ///
    /// Read off <see cref="ToToml"/> rather than off the records, because the resolved TOML is what
    /// a reader six months from now will compare, and because it catches a difference that no
    /// single overridden key explains — a scenario that moves `households` also moves six derived
    /// capacities, and a comparison that only knew about the key that was written would call those
    /// six a surprise.
    /// </summary>
    public IReadOnlyList<string> DifferencesFrom(SimulationParameters other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var mine = Resolved(ToToml());
        var theirs = Resolved(other.ToToml());

        return
        [
            .. mine.Keys
                .Union(theirs.Keys, StringComparer.Ordinal)
                .Where(key => !string.Equals(Value(mine, key), Value(theirs, key), StringComparison.Ordinal))
                .OrderBy(key => key, StringComparer.Ordinal),
        ];
    }

    private static string Value(IReadOnlyDictionary<string, string> from, string key) =>
        from.TryGetValue(key, out var value) ? value : "absent";

    private static Dictionary<string, string> Resolved(string toml)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = string.Empty;

        foreach (var raw in toml.Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line[0] == '[')
            {
                section = line.Trim('[', ']');
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator < 1)
            {
                continue;
            }

            values[section + "." + line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return values;
    }

    private static void Write(StringBuilder toml, string key, int value) =>
        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = {value}");

    private static void Write(StringBuilder toml, string key, bool value) =>
        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = {(value ? "true" : "false")}");

    private static void Write(StringBuilder toml, string key, string value) =>
        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = \"{value}\"");

    private static void Write(StringBuilder toml, string key, double value) =>
        toml.AppendLine(
            CultureInfo.InvariantCulture,
            $"{key} = {value.ToString("0.0###", CultureInfo.InvariantCulture)}");

    /// <summary>An inline table of per-category numbers, in name order: `w = { food = 1.0, … }`.</summary>
    private static void WriteWeights(StringBuilder toml, string key, IReadOnlyList<CategoryWeight> weights)
    {
        var entries = weights.Select(w =>
            string.Create(CultureInfo.InvariantCulture, $"{w.Category} = {RoundTrip(w.Value)}"));

        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = {{ {string.Join(", ", entries)} }}");
    }

    /// <summary>
    /// A weight printed so that reading it back gives the same double, to the bit.
    ///
    /// Every other number in this file is printed to a few decimals, because every other number was
    /// typed by a person. A normalised weight was *computed* — 0.90 / 1.015 — and the effective
    /// configuration is the record of what ran, so it is printed round-trip. Truncating it would
    /// mean a run reloaded from its own effective config was a slightly different run, and the
    /// difference would be invisible in exactly the comparison V5a exists to make.
    ///
    /// An integral value keeps its `.0`, so that the identity table reads as `1.0` rather than as
    /// the integer `1`.
    /// </summary>
    /// <summary>A number printed so that reading it back gives the same double, to the bit.</summary>
    private static void WriteExactly(StringBuilder toml, string key, double value) =>
        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = {RoundTrip(value)}");

    private static string RoundTrip(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Money is written in euros, because that is how the specification quotes it.</summary>
    private static void Write(StringBuilder toml, string key, Money value) =>
        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = {value.ToCsv()}");
}
