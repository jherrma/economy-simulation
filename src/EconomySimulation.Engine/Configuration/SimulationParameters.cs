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

    // ---- derived money quantities (§7) ---------------------------------------------------

    /// <summary>`households × mean_income × opening_cash_share` — €650,000 at the defaults.</summary>
    public Money OpeningHouseholdCash =>
        Income.MeanIncome.Scaled(Income.OpeningCashShare) * Run.Households;

    /// <summary>
    /// Twelve months of total income. The pool has no behaviour and its size is not economically
    /// meaningful; it is a buffer that makes the warm-up transient survivable. One month would
    /// halt the run around tick 11.
    /// </summary>
    public Money OpeningPool =>
        Income.MeanIncome * Run.Households * Money.OpeningPoolMonths;

    /// <summary>
    /// M0 — €8,450,000 at the defaults. Fixed for the run; only lending and repayment change the
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
