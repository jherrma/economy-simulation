namespace EconomySimulation.Engine.Configuration;

/// <summary>§1 of spec/02-PARAMETERS.md.</summary>
public sealed record RunParameters
{
    /// <summary>Large enough for cohort statistics, small enough to run in a second.</summary>
    public int Households { get; init; } = 1000;

    /// <summary>The measured cohort. θ = 0 for these households in every scenario.</summary>
    public double AbstainerShare { get; init; } = 0.20;

    /// <summary>One tick is one month; fifty years, of which the last thirty are measured.</summary>
    public int Ticks { get; init; } = 600;

    /// <summary>
    /// Written to output but flagged, never silently discarded. Long, because the opening tier
    /// prices are deliberately not an equilibrium.
    ///
    /// Two hundred and forty rather than a hundred and twenty **on the null run's evidence**
    /// (`01-SIMULATION.md` §10.3). The price *level* settles in about thirty ticks; **relative**
    /// prices take eight times as long. Leisure's budget shelf opens at €120 and converges to
    /// €108.3 with a time constant near seventy ticks, so at tick 120 it is still four and a half
    /// per cent above where it is going — a drift that sat inside the old measured window at
    /// thirteen standard errors and would have been read as a result.
    /// </summary>
    public int WarmupTicks { get; init; } = 240;

    /// <summary>The same thirty in every scenario, compared paired.</summary>
    public int Seeds { get; init; } = 30;

    /// <summary>
    /// What to call this run in the output. A label and nothing else: no part of the model reads
    /// it, and setting it does not change a single draw.
    ///
    /// It exists because E9 concatenates the files of hundreds of runs, and a row that cannot say
    /// which scenario it came from is a row that will be averaged into the wrong one. The default
    /// is the baseline's name, because the default configuration *is* the baseline.
    /// </summary>
    public string Scenario { get; init; } = "credit_off";
}

/// <summary>§2.</summary>
public sealed record IncomeParameters
{
    /// <summary>
    /// **Discretionary** income per month — after housing, transport, insurance, health and tax,
    /// none of which exist in this model.
    /// </summary>
    public Money MeanIncome { get; init; } = Money.FromEuros(650);

    /// <summary>Lognormal spread, wide enough that the tier ladder is populated at both ends.</summary>
    public double SigmaIncome { get; init; } = 0.35;

    public double OpeningCashShare { get; init; } = 1.00;
}

/// <summary>
/// One row of the goods table — §3.1 called it a category, §3.6 calls it a **good**.
///
/// The two names are the same thing and the record keeps the older one, because the TOML section is
/// `[categories]` and that spelling is frozen: V5a's fixture is never regenerated and has to keep
/// loading through this schema. What changed in E11 is that a row now carries a <see cref="Category"/>
/// label, and rows sharing a label are a category. **Nothing else defines one** — there is no group
/// type, no nesting and no second index, because everything a group has (a life, a price, a
/// capacity, a value weight, a financeable flag) is what this row already is
/// (`01-SIMULATION.md` §5.5).
/// </summary>
public sealed record CategoryParameters
{
    public required string Name { get; init; }

    /// <summary>
    /// What this row is grouped under for reporting, and nothing more. **Absent means the row's own
    /// name**, so §3.1's six rows are six categories of one good each and every file written before
    /// E11 means exactly what it always meant.
    ///
    /// It is a label rather than a thing: the engine never branches on it, no decision reads it, and
    /// the only code that looks at it is the output roll-up (`01-SIMULATION.md` §7).
    ///
    /// **The loader always resolves it**, so a row that came from a configuration carries its label
    /// spelled out and two rows are equal when they say the same thing. Only a row built by hand in
    /// a test leaves it empty, and such a row should be read through <see cref="Label"/>.
    /// </summary>
    public string Category { get; init; } = "";

    /// <summary>The category this row rolls up into — <see cref="Category"/>, or the row's own name.</summary>
    public string Label => Category.Length == 0 ? Name : Category;

    /// <summary>Ticks a unit lasts. 1 means it is consumed every tick.</summary>
    public required int Life { get; init; }

    /// <summary>
    /// Units produced per tick: `round(households / life)`, the steady-state replacement demand,
    /// which is the only non-arbitrary way to size it.
    /// </summary>
    public required int Capacity { get; init; }

    /// <summary>The standard tier's opening price. The other two are derived from it.</summary>
    public required Money PriceRef { get; init; }

    /// <summary>
    /// What a candidate in this good scores for a household at the mean income, before the tier
    /// multipliers — the number a reader can argue with, and the one §3.6 authors.
    ///
    /// Nobody has an intuition about `v = 0.0141`. Everybody has one about "this good scores 1.10
    /// at the mean income", because it says directly where the good sits relative to λ, and the
    /// tier a median household reaches follows from it by arithmetic: × 1.133 for the budget unit,
    /// × 0.800 for the standard upgrade, × 0.500 for premium, all three read off the tier table.
    ///
    /// **Zero means the row authors <see cref="V"/> directly instead**, which is what §3.1's six
    /// rows do and will keep doing. It is not a flag but the absence of a number: the good still
    /// has a base score either way, and it is `Valuation.Score` of the base value over the flow
    /// cost at the mean income, which is where that arithmetic lives and stays.
    /// </summary>
    public double BaseScore { get; init; }

    /// <summary>
    /// The good's share of a mean household's flow value. **Derived from <see cref="BaseScore"/>
    /// where that is authored**, and a stated `v` must then equal the derived value or the run is
    /// refused — the rule `capacity` already obeys.
    /// </summary>
    public required double V { get; init; }

    /// <summary>How much of `v` does **not** scale with income. This is Engel's law.</summary>
    public required double Necessity { get; init; }

    public required bool Financeable { get; init; }

    /// <summary>Loan term in months. Zero where the category cannot be financed.</summary>
    public required int Term { get; init; }

    /// <summary>
    /// The Stone-Geary floor, in euros per tick: `necessity · v · mean_income`.
    ///
    /// Computed, never typed in. Without the split, willingness to pay is proportional to income
    /// and food becomes a luxury — a household on €450 scores food at 0.93 and buys none. The
    /// poor end of the distribution starves and no invariant in the model would catch it.
    /// </summary>
    public Money Floor(Money meanIncome) => meanIncome.Scaled(Necessity * V);

    /// <summary>The income-linked part: `(1 − necessity) · v`.</summary>
    public double IncomeSlope => (1.0 - Necessity) * V;

    /// <summary>
    /// `v_g = base_score_g · price_ref_g / (life_g · mean_income)` — the derivation, in one place.
    ///
    /// **Nominal neutrality survives it** (V3): `price_ref` and `mean_income` are both money and
    /// scale together, so `v` is invariant under a change of unit. That is the whole reason the
    /// base score can be authored at all — a scored quantity that moved when the currency was
    /// redenominated would be a parameter of the numeraire rather than of the household.
    /// </summary>
    public double DerivedV(Money meanIncome) => DeriveV(BaseScore, PriceRef, Life, meanIncome);

    /// <summary>The same, before there is a row to hang it on.</summary>
    public static double DeriveV(double baseScore, Money priceRef, int life, Money meanIncome) =>
        baseScore * priceRef.Cents / (life * (double)meanIncome.Cents);

    /// <summary>The specification's six categories, in the order §3.1 lists them.</summary>
    public static IReadOnlyList<CategoryParameters> Default { get; } =
    [
        new() { Name = "food",        Category = "food",        Life = 1,  Capacity = 1000, PriceRef = Money.FromEuros(300), V = 0.620, Necessity = 0.70, Financeable = false, Term = 0 },
        new() { Name = "leisure",     Category = "leisure",     Life = 1,  Capacity = 1000, PriceRef = Money.FromEuros(200), V = 0.400, Necessity = 0.35, Financeable = false, Term = 0 },
        new() { Name = "clothing",    Category = "clothing",    Life = 6,  Capacity = 167,  PriceRef = Money.FromEuros(400), V = 0.110, Necessity = 0.50, Financeable = false, Term = 0 },
        new() { Name = "hobby",       Category = "hobby",       Life = 12, Capacity = 83,   PriceRef = Money.FromEuros(600), V = 0.088, Necessity = 0.15, Financeable = true,  Term = 12 },
        new() { Name = "electronics", Category = "electronics", Life = 36, Capacity = 28,   PriceRef = Money.FromEuros(900), V = 0.048, Necessity = 0.25, Financeable = true,  Term = 24 },
        new() { Name = "appliances",  Category = "appliances",  Life = 96, Capacity = 10,   PriceRef = Money.FromEuros(800), V = 0.016, Necessity = 0.45, Financeable = true,  Term = 24 },
    ];
}

/// <summary>§3.2 — one of the three quality tiers.</summary>
public sealed record TierParameters
{
    public required string Name { get; init; }

    public required double PriceMult { get; init; }

    /// <summary>
    /// Rises more slowly than <see cref="PriceMult"/>, always. That single property is what makes
    /// quality behave like quality: each step up is worth having and each step up is worse value
    /// for money than the one below it. Diminishing returns are a consequence, not a parameter.
    /// </summary>
    public required double ValueMult { get; init; }

    /// <summary>
    /// The opening share of a category's capacity, **in units**. Splitting units rather than
    /// value is what keeps the calibration exact.
    /// </summary>
    public required double UnitShare { get; init; }

    public static IReadOnlyList<TierParameters> Default { get; } =
    [
        new() { Name = "budget",   PriceMult = 0.60, ValueMult = 0.68, UnitShare = 0.40 },
        new() { Name = "standard", PriceMult = 1.00, ValueMult = 1.00, UnitShare = 0.40 },
        new() { Name = "premium",  PriceMult = 1.80, ValueMult = 1.40, UnitShare = 0.20 },
    ];
}

/// <summary>§4.</summary>
public sealed record DecisionParameters
{
    /// <summary>Take a candidate worth at least what it costs. A pure number, never indexed.</summary>
    public double Lambda { get; init; } = 1.00;

    /// <summary>Spread of the household taste multiplier `w_h`, mean exactly 1.</summary>
    public double SigmaW { get; init; } = 0.20;

    /// <summary>Protected from the credit residual test: 0.55 × 650 = €357.50.</summary>
    public double SubsistenceShare { get; init; } = 0.55;

    /// <summary>
    /// φ — months of own income a household holds before cash starts lowering its λ:
    /// `λ_h = λ · min(1, φ / b_h)`, `b_h = cash_h / income_h`. The reservation price on money
    /// (`01-SIMULATION.md` §5.3). Below φ months λ is unchanged, so nobody skips a meal to build a
    /// buffer; above it hoarded cash is spent into quality, which is what anchors the price level.
    /// </summary>
    public double BufferMonths { get; init; } = 2.0;

    /// <summary>Myopic by default: the instalment must fit **this** tick.</summary>
    public AffordabilityHorizon AffordabilityHorizon { get; init; } = AffordabilityHorizon.Myopic;
}

/// <summary>§5.</summary>
public sealed record CreditParameters
{
    /// <summary>Off, so that the default configuration *is* the baseline.</summary>
    public bool CreditEnabled { get; init; }

    /// <summary>German consumer instalment credit, nominal, simple interest.</summary>
    public Rate LoanRate { get; init; } = new(8.0);

    /// <summary>
    /// Loans create deposits. Setting this false funds them from the pool instead, and the
    /// difference between the two runs is the money-creation channel measured directly.
    /// </summary>
    public bool MoneyCreation { get; init; } = true;

    /// <summary>
    /// θ_h is uniform on [min, max]. The default pair is `theta_low`; θ is drawn in every
    /// scenario, including the baseline where it is never read, because the draw has to happen in
    /// the same place in the same stream or the paired comparison is between two random worlds.
    /// </summary>
    public double ThetaMin { get; init; }

    public double ThetaMax { get; init; } = 0.2;
}

/// <summary>§6.</summary>
public sealed record PriceParameters
{
    /// <summary>Adjustment speed, per tier. A 20% shortage moves that tier's price 1% next tick.</summary>
    public double K { get; init; } = 0.05;

    /// <summary>
    /// Stops a tier priced to zero from dividing by zero downstream. **Scales under the
    /// neutrality test** — a fixed floor would break V3.
    /// </summary>
    public Money PriceFloor { get; init; } = Money.FromEuros(1);

    public Rationing Rationing { get; init; } = Rationing.Random;
}

/// <summary>§7.</summary>
public sealed record MoneyParameters
{
    /// <summary>
    /// Twenty-four months of total income in the pool. The warm-up transient drains it while prices
    /// find the tier mix; one month would halt the run around tick 11.
    ///
    /// Twenty-four rather than twelve because the run is now six hundred ticks and the drain does
    /// not stop: a residual of about 1.9% of one tick's income per tick continues for the whole run
    /// and is hoarding by the top decile alone (`03-VERIFICATION.md` §V4). Twelve months buys about
    /// seven hundred ticks; the worst of thirty seeds halted at tick 697. The pool has no behaviour,
    /// so enlarging the buffer changes nothing in the economy — only whether the run reaches its
    /// end.
    /// </summary>
    public int OpeningPoolMonths { get; init; } = 24;
}

/// <summary>Whether a financed instalment has to fit this tick or the whole term.</summary>
public enum AffordabilityHorizon
{
    /// <summary>The instalment must fit this tick. The specification's default.</summary>
    Myopic,

    /// <summary>The control: the household checks it can carry the loan to the end.</summary>
    FullTerm,
}

/// <summary>Who gets the last unit when a tier sells out.</summary>
public enum Rationing
{
    /// <summary>First come, within a seeded random household order.</summary>
    Random,

    /// <summary>A variant, expected to strengthen the result, reported separately.</summary>
    Willingness,
}

/// <summary>The TOML spellings of the enumerated parameters, in one place, both ways.</summary>
public static class EnumeratedParameters
{
    // CS8524 is "all named values are covered, but the input could still hold a value cast in
    // from outside the enum". A `_ =>` arm would silence it — and would silence CS8509 with it,
    // which is the error that fires when a *named* value is missing and the whole reason
    // spec/stories/01-01 tests for it. So CS8524 is disabled here and CS8509 is left armed:
    // adding a tier of affordability horizon still breaks the build, which is the point.
    // EnumSwitchTests proves that is still true with this pragma in force.
#pragma warning disable CS8524
    public static string ToTomlValue(this AffordabilityHorizon horizon) => horizon switch
    {
        AffordabilityHorizon.Myopic => "myopic",
        AffordabilityHorizon.FullTerm => "full_term",
    };

    public static string ToTomlValue(this Rationing rationing) => rationing switch
    {
        Rationing.Random => "random",
        Rationing.Willingness => "willingness",
    };

#pragma warning restore CS8524

    public static IReadOnlyList<string> AffordabilityHorizons { get; } = ["myopic", "full_term"];

    public static IReadOnlyList<string> Rationings { get; } = ["random", "willingness"];
}
