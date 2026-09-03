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
        toml.AppendLine();

        toml.AppendLine("[income]");
        Write(toml, "mean_income", Income.MeanIncome);
        Write(toml, "sigma_income", Income.SigmaIncome);
        Write(toml, "opening_cash_share", Income.OpeningCashShare);
        toml.AppendLine();

        foreach (var category in Categories)
        {
            toml.AppendLine(CultureInfo.InvariantCulture, $"[categories.{category.Name}]");
            Write(toml, "life", category.Life);
            Write(toml, "capacity", category.Capacity);
            Write(toml, "price_ref", category.PriceRef);
            Write(toml, "v", category.V);
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

    /// <summary>Money is written in euros, because that is how the specification quotes it.</summary>
    private static void Write(StringBuilder toml, string key, Money value) =>
        toml.AppendLine(CultureInfo.InvariantCulture, $"{key} = {value.ToCsv()}");
}
