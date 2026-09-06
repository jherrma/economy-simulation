using System.Globalization;
using FluentResults;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;
using static System.FormattableString;

namespace EconomySimulation.Engine.Configuration;

/// <summary>
/// TOML in, schema out, as a <see cref="Result"/>. Nothing here throws for bad input: a
/// configuration file is something a person wrote, and being wrong is an outcome rather than a bug.
///
/// Every problem is collected and reported together. That is the difference between a bad config
/// costing one edit and costing a dozen run-fix-rerun cycles.
/// </summary>
public static class ConfigurationLoader
{
    public static Result<SimulationParameters> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            return Result.Fail<SimulationParameters>(
                $"configuration file: expected a readable path, got {path}");
        }

        return FromToml(File.ReadAllText(path));
    }

    public static Result<SimulationParameters> FromToml(string toml) =>
        FromToml(toml, SimulationParameters.Default);

    /// <summary>
    /// The same load, overlaid on something other than the specification's defaults.
    ///
    /// This is what a scenario is: a partial file on top of a basis, with absent keys taken from
    /// the basis rather than defaulted back to the schema. The distinction only shows when the
    /// basis is not the defaults — a gate that has already scaled every price, or shortened the
    /// run, needs the scenario's credit settings applied to *its* parameters and everything else
    /// left where it put it.
    /// </summary>
    public static Result<SimulationParameters> FromToml(string toml, SimulationParameters basis)
    {
        ArgumentNullException.ThrowIfNull(basis);

        TomlTable root;

        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(toml) ?? [];
        }
        catch (TomlException malformed)
        {
            // The one place this file catches: a person wrote the file, so being wrong is an
            // outcome rather than a bug, and it leaves here as a Result like every other outcome.
            var syntax = new Validation();

            foreach (var diagnostic in malformed.Diagnostics)
            {
                syntax.Fail(
                    $"configuration, line {diagnostic.Span.Start.Line + 1}",
                    "valid TOML",
                    diagnostic.Message);
            }

            if (!syntax.HasFailures)
            {
                syntax.Fail("configuration", "valid TOML", malformed.Message);
            }

            return syntax.ToResult<SimulationParameters>(() => basis);
        }

        var problems = new Validation();
        var defaults = basis;

        var run = ReadRun(Section(root, "run", problems), defaults.Run);
        var income = ReadIncome(Section(root, "income", problems), defaults.Income);
        var categories = ReadCategories(Section(root, "categories", problems), run, income, problems, basis);
        var tiers = ReadTiers(Section(root, "tiers", problems), problems, basis);
        var archetypes = ReadArchetypes(Section(root, "archetypes", problems), problems, basis, categories);
        var decision = ReadDecision(Section(root, "decision", problems), defaults.Decision);
        var credit = ReadCredit(Section(root, "credit", problems), defaults.Credit);
        var prices = ReadPrices(Section(root, "prices", problems), defaults.Prices);
        var money = ReadMoney(Section(root, "money", problems), defaults.Money);

        foreach (var key in root.Keys.Where(k => !KnownSections.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            problems.Fail(
                key,
                "one of " + string.Join(", ", KnownSections),
                "an unknown section");
        }

        var parameters = new SimulationParameters
        {
            Run = run,
            Income = income,
            Categories = categories,
            Tiers = tiers,
            Archetypes = archetypes,
            Decision = decision,
            Credit = credit,
            Prices = prices,
            Money = money,
        };

        Check(parameters, problems);

        return problems.ToResult(() => parameters);
    }

    /// <summary>
    /// Writes the **resolved** configuration beside a run's output — the input file with every
    /// absent key filled in.
    ///
    /// Writing the input file instead is what saves nothing six months from now. Defaults change,
    /// and a CSV whose parameters cannot be recovered is not a result.
    /// </summary>
    public static Result WriteEffectiveConfiguration(SimulationParameters parameters, string directory)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "effective-config.toml"), parameters.ToToml());
            return Results.Ok;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(
                $"effective configuration: expected to write into {directory}, got {failure.Message}");
        }
    }

    private static readonly string[] KnownSections =
        ["run", "income", "categories", "tiers", "archetypes", "decision", "credit", "prices", "money"];

    private static TomlSection Section(TomlTable root, string name, Validation problems)
    {
        var table = root.TryGetValue(name, out var raw) && raw is TomlTable found ? found : [];

        return new TomlSection(table, name, problems);
    }

    // ---- sections --------------------------------------------------------------------------

    private static RunParameters ReadRun(TomlSection section, RunParameters defaults)
    {
        var run = new RunParameters
        {
            Households = section.Int("households", defaults.Households),
            AbstainerShare = section.Double("abstainer_share", defaults.AbstainerShare),
            Ticks = section.Int("ticks", defaults.Ticks),
            WarmupTicks = section.Int("warmup_ticks", defaults.WarmupTicks),
            Seeds = section.Int("seeds", defaults.Seeds),
            Scenario = section.Text("scenario", defaults.Scenario),
            MinShelfUnits = section.Int("min_shelf_units", defaults.MinShelfUnits),
            Replacement = section.Choice(
                "replacement",
                defaults.Replacement.ToTomlValue(),
                EnumeratedParameters.Replacements) switch
            {
                "hazard" => Replacement.Hazard,
                _ => Replacement.Deterministic,
            },
        };

        section.RejectUnknownKeys();
        return run;
    }

    private static IncomeParameters ReadIncome(TomlSection section, IncomeParameters defaults)
    {
        var income = new IncomeParameters
        {
            MeanIncome = section.Money("mean_income", defaults.MeanIncome),
            SigmaIncome = section.Double("sigma_income", defaults.SigmaIncome),
            OpeningCashShare = section.Double("opening_cash_share", defaults.OpeningCashShare),
        };

        section.RejectUnknownKeys();
        return income;
    }

    /// <summary>
    /// The goods table, as an **overlay** on the specification's six rows — unless the file says
    /// `replace = true`, and then as the whole table.
    ///
    /// A `[categories.food]` table changes food and leaves the other five alone. That is the right
    /// default and it stays the default: a file that mentioned one category and silently got a
    /// one-category economy would run, and produce a number, and look like the run that was asked
    /// for. A name the specification does not know is a new row, and then every field is the file's
    /// responsibility. That is how a seventh row is added without touching the engine.
    ///
    /// **`replace = true` is what a second calibration needs**, and E11 is why it exists. §3.6's
    /// eighteen goods are not an edit to §3.1's six — they are a different table for the same town,
    /// and overlaid they load as *twenty-four* rows whose `Σ price_ref / life` comes to 1,300
    /// against a mean income of 650. There is no removal syntax and inventing one would be worse:
    /// six `[categories.food] delete = true` stanzas is a file that says what it is not. Saying
    /// "this section is the table" once, at the top, is a file that says what it is.
    ///
    /// It is stated per file rather than inferred from how many rows are present, because inferring
    /// it is the failure: a file naming eighteen goods is indistinguishable from a file editing
    /// eighteen of them, and guessing wrong is silent in both directions.
    /// </summary>
    private static IReadOnlyList<CategoryParameters> ReadCategories(
        TomlSection section,
        RunParameters run,
        IncomeParameters income,
        Validation problems,
        SimulationParameters basis)
    {
        // The scalar before Subtables(), which ignores what has already been read.
        var replace = section.Bool("replace", false);
        var stated = section.SubtablesInFileOrder();
        var categories = new List<CategoryParameters>();
        IReadOnlyList<CategoryParameters> inherited = replace ? [] : basis.Categories;

        foreach (var name in inherited.Select(c => c.Name).Concat(stated).Distinct(StringComparer.Ordinal))
        {
            var known = inherited.FirstOrDefault(c => c.Name == name);
            var table = stated.Contains(name, StringComparer.Ordinal) ? section.Subtable(name) : null;

            if (table is null && known is null)
            {
                continue;
            }

            if (table is null)
            {
                // Untouched by the file, but capacity still follows this run's household count.
                categories.Add(known! with { Capacity = DerivedCapacity(run.Households, known!.Life) });
                continue;
            }

            var row = new TomlSection(table, $"categories.{name}", problems);
            var life = row.Int("life", known?.Life ?? 0);
            var priceRef = row.Money("price_ref", known?.PriceRef ?? Engine.Money.Zero);
            var baseScore = row.Double("base_score", known?.BaseScore ?? 0.0);

            // `v` is derived from the base score wherever one is authored, and re-derived here
            // rather than inherited: an overlay that moves `price_ref` and kept the basis's `v`
            // would be a row whose score silently stopped being the score it states.
            // A life of zero is a row that is broken for a different reason, and CheckCategories is
            // already going to say so; dividing by it here would add a second complaint about one
            // mistake, and an infinite `v` besides.
            var derived = baseScore > 0.0 && life >= 1
                ? CategoryParameters.DeriveV(baseScore, priceRef, life, income.MeanIncome)
                : known?.V ?? 0.0;

            var category = new CategoryParameters
            {
                Name = name,
                // Resolved here and nowhere else, so every row that came from a file carries its
                // label spelled out and two tables are equal when they say the same thing.
                Category = row.Text("category", known?.Label ?? name),
                Life = life,
                // capacity is round(households / life) by definition, so it is derived unless the
                // file states it — and if it does, Check makes sure it states the right value.
                Capacity = row.Int("capacity", DerivedCapacity(run.Households, life)),
                PriceRef = priceRef,
                BaseScore = baseScore,
                V = row.Double("v", derived),
                Necessity = row.Double("necessity", known?.Necessity ?? 0.0),
                Financeable = row.Bool("financeable", known?.Financeable ?? false),
                Term = row.Int("term", known?.Term ?? 0),
            };

            row.RejectUnknownKeys();
            categories.Add(category);
        }

        return categories;
    }

    /// <summary>
    /// The tiers, overlaid the same way, then ordered by price. The ladder is defined by what
    /// things cost, not by the order somebody happened to write the tables in.
    /// </summary>
    private static IReadOnlyList<TierParameters> ReadTiers(
        TomlSection section,
        Validation problems,
        SimulationParameters basis)
    {
        var stated = section.Subtables();
        var tiers = new List<TierParameters>();

        foreach (var name in basis.Tiers.Select(t => t.Name).Concat(stated).Distinct(StringComparer.Ordinal))
        {
            var known = basis.Tiers.FirstOrDefault(t => t.Name == name);
            var table = stated.Contains(name, StringComparer.Ordinal) ? section.Subtable(name) : null;

            if (table is null && known is null)
            {
                continue;
            }

            if (table is null)
            {
                tiers.Add(known!);
                continue;
            }

            var row = new TomlSection(table, $"tiers.{name}", problems);

            var tier = new TierParameters
            {
                Name = name,
                PriceMult = row.Double("price_mult", known?.PriceMult ?? 0.0),
                ValueMult = row.Double("value_mult", known?.ValueMult ?? 0.0),
                UnitShare = row.Double("unit_share", known?.UnitShare ?? 0.0),
            };

            row.RejectUnknownKeys();
            tiers.Add(tier);
        }

        return [.. tiers.OrderBy(t => t.PriceMult)];
    }

    /// <summary>
    /// The archetype table (§3.5), authored as **relative** weights and normalised here.
    ///
    /// Writing a table whose columns already average to one by hand is arithmetic busywork that
    /// hides what the author meant, so the file states what makes a type different and the loader
    /// does the division. That has a consequence worth being deliberate about: what the author
    /// wrote and what the model ran are then different numbers, and it is the second that goes into
    /// the effective configuration — which is what the campaign manifest hashes. Same reasoning as
    /// `capacity` in §3.1: derive what is derivable, and record the derived value where a reader
    /// will find it.
    ///
    /// Unlike categories and tiers this is **not** an overlay per type. A file that names any
    /// archetype states the whole table, because the shares have to sum to one and a table half
    /// from the basis and half from the file would be a population nobody wrote down.
    /// </summary>
    private static ArchetypeParameters ReadArchetypes(
        TomlSection section,
        Validation problems,
        SimulationParameters basis,
        IReadOnlyList<CategoryParameters> categories)
    {
        // Scalars before Subtables(), which ignores what has already been read.
        var sigmaIdio = section.Double("sigma_idio", basis.Archetypes.SigmaIdio);
        var normaliseKappa = section.Bool("normalise_kappa", basis.Archetypes.NormaliseKappa);
        var stated = section.Subtables();
        var goods = categories.Select(c => c.Name).ToArray();

        // Taste is authored per **label** and the cycle per **good** (§3.5, §3.7). Distinct labels
        // rather than one per row: `w = { electronics = 1.60 }` is one statement about a category,
        // and under §3.1 the two lists are the same six strings.
        var labels = categories
            .Select(c => c.Label)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var authored = stated.Count == 0
            ? basis.Archetypes.Types
            : stated
                .Select(name => ReadArchetype(section, name, labels, goods, problems))
                .Where(type => type is not null)
                .Select(type => type!)
                .ToArray();

        section.RejectUnknownKeys();

        return new ArchetypeParameters
        {
            SigmaIdio = sigmaIdio,
            NormaliseKappa = normaliseKappa,
            Types = Normalise(authored, labels, goods, normaliseKappa, problems),
        };
    }

    private static Archetype? ReadArchetype(
        TomlSection section,
        string name,
        IReadOnlyList<string> labels,
        IReadOnlyList<string> goods,
        Validation problems)
    {
        var table = section.Subtable(name);

        if (table is null)
        {
            return null;
        }

        var row = new TomlSection(table, $"archetypes.{name}", problems);
        var share = row.Double("share", 0.0);
        var w = ReadWeights(row, "w", $"archetypes.{name}.w", labels, problems);
        var kappa = ReadWeights(row, "kappa", $"archetypes.{name}.kappa", labels, problems);
        var d = ReadWeights(row, "d", $"archetypes.{name}.d", goods, problems);

        row.RejectUnknownKeys();

        return new Archetype { Name = name, Share = share, W = w, Kappa = kappa, D = d };
    }

    /// <summary>
    /// One `w`, `kappa` or `d` row: a number per key, **absent meaning 1.0** and an unknown key an
    /// error. The keys are category labels for `w` and `kappa`, goods-table rows for `d`.
    ///
    /// That asymmetry is 02-02's, inherited rather than re-implemented — reading the row through a
    /// <see cref="TomlSection"/> is what buys it. A type that is average in leisure should not have
    /// to say so; a type that says `leasure` must not quietly be average in leisure too.
    /// </summary>
    private static IReadOnlyList<CategoryWeight> ReadWeights(
        TomlSection row,
        string key,
        string path,
        IReadOnlyList<string> categories,
        Validation problems)
    {
        var table = row.Subtable(key);

        if (table is null)
        {
            return Archetype.Ones(categories);
        }

        var weights = new TomlSection(table, path, problems);

        var stated = categories
            .OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => new CategoryWeight(c, weights.Double(c, 1.0)))
            .ToArray();

        weights.RejectUnknownKeys();

        return stated;
    }

    /// <summary>
    /// Normalises the table: two identities, conserving two different things.
    ///
    /// **`w`, arithmetically.** Each column is divided by its share-weighted mean, so that
    /// `Σ_A share_A · m_g,A = 1` holds by construction (§3.5). The identity is what keeps `v_g`
    /// meaning what it says: a table redistributes a category's demand across the population, it
    /// does not change how much of it there is.
    ///
    /// **`d`, harmonically.** Each column is *multiplied* by `Σ_A share_A / d[A][g]`, so that
    /// `Σ_A share_A / d[A][g] = 1` holds instead. Demand per tick is `1 / life`, so what has to
    /// average to one is the **reciprocal**, and this is not a stylistic preference. Normalise `d`
    /// itself arithmetically and Jensen's inequality — `E[1/d] > 1/E[d]` for any `d` that varies at
    /// all — hands the population strictly more replacement demand than `capacity` was sized for,
    /// permanently, in every good, in every run. The reprice rule absorbs it into the price level,
    /// no conservation identity notices, and the town is simply a little tighter than the goods
    /// table says it is. The multiplication rather than the division is the whole of the difference,
    /// and ReplacementCycleTests exists to fail if it is ever turned back around.
    ///
    /// Applied to a table that already satisfies either identity, the scale is exactly 1 and nothing
    /// moves — which is why the identity table survives this untouched, and why V5a can be a
    /// byte-for-byte comparison.
    ///
    /// `kappa` gets no such treatment: see <see cref="Archetype.Kappa"/>.
    /// </summary>
    private static IReadOnlyList<Archetype> Normalise(
        IReadOnlyList<Archetype> authored,
        IReadOnlyList<string> labels,
        IReadOnlyList<string> goods,
        bool normaliseKappa,
        Validation problems)
    {
        if (authored.Count == 0)
        {
            problems.Fail("archetypes", "at least one household type", "none");
            return authored;
        }

        var scales = new Dictionary<string, double>(StringComparer.Ordinal);
        var kappaScales = new Dictionary<string, double>(StringComparer.Ordinal);
        var cycleScales = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var label in labels)
        {
            kappaScales[label] = normaliseKappa
                ? Scale(authored.Sum(a => a.Share * a.ExponentFor(label)))
                : 1.0;

            var scale = authored.Sum(a => a.Share * a.WeightFor(label));

            problems.Require(
                scale > 0.0 && double.IsFinite(scale),
                $"archetypes.w.{label}",
                "a positive share-weighted mean — the column is divided by it, and a column of "
                + "zeros is a category nobody wants at any price",
                Format(scale));

            scales[label] = Scale(scale);
        }

        foreach (var good in goods)
        {
            // A cycle of zero or less is not a shorter life, it is a division by zero one step
            // later. Caught here rather than in the sum, so the message names the multiplier.
            var usable = authored.All(a => a.CycleFor(good) > 0.0 && double.IsFinite(a.CycleFor(good)));

            problems.Require(
                usable,
                $"archetypes.d.{good}",
                "a positive replacement-cycle multiplier from every type — the life is multiplied "
                + "by it and the per-tick failure probability is its reciprocal",
                Format(authored.Min(a => a.CycleFor(good))));

            cycleScales[good] = usable
                ? Scale(authored.Sum(a => a.Share / a.CycleFor(good)))
                : 1.0;
        }

        return
        [
            .. authored.Select(a => a with
            {
                W =
                [
                    .. labels
                        .OrderBy(c => c, StringComparer.Ordinal)
                        .Select(c => new CategoryWeight(c, a.WeightFor(c) / scales[c])),
                ],
                Kappa =
                [
                    .. labels
                        .OrderBy(c => c, StringComparer.Ordinal)
                        .Select(c => new CategoryWeight(c, a.ExponentFor(c) / kappaScales[c])),
                ],

                // Multiplied, where `w` is divided. `Σ share / (d · k) = (1 / k) · Σ share / d`, so
                // scaling *up* by the reciprocal sum is what drives that sum to one.
                D =
                [
                    .. goods
                        .OrderBy(c => c, StringComparer.Ordinal)
                        .Select(c => new CategoryWeight(c, a.CycleFor(c) * cycleScales[c])),
                ],
            }),
        ];
    }

    private static DecisionParameters ReadDecision(TomlSection section, DecisionParameters defaults)
    {
        var decision = new DecisionParameters
        {
            Lambda = section.Double("lambda", defaults.Lambda),
            SigmaW = section.Double("sigma_w", defaults.SigmaW),
            SubsistenceShare = section.Double("subsistence_share", defaults.SubsistenceShare),
            BufferMonths = section.Double("buffer_months", defaults.BufferMonths),
            AffordabilityHorizon = section.Choice(
                "affordability_horizon",
                defaults.AffordabilityHorizon.ToTomlValue(),
                EnumeratedParameters.AffordabilityHorizons) switch
            {
                "full_term" => AffordabilityHorizon.FullTerm,
                _ => AffordabilityHorizon.Myopic,
            },
        };

        section.RejectUnknownKeys();
        return decision;
    }

    private static CreditParameters ReadCredit(TomlSection section, CreditParameters defaults)
    {
        var credit = new CreditParameters
        {
            CreditEnabled = section.Bool("credit_enabled", defaults.CreditEnabled),
            LoanRate = new Rate(section.Double("loan_rate", defaults.LoanRate.PercentPerAnnum)),
            MoneyCreation = section.Bool("money_creation", defaults.MoneyCreation),
            ThetaMin = section.Double("theta_min", defaults.ThetaMin),
            ThetaMax = section.Double("theta_max", defaults.ThetaMax),
        };

        section.RejectUnknownKeys();
        return credit;
    }

    private static PriceParameters ReadPrices(TomlSection section, PriceParameters defaults)
    {
        var prices = new PriceParameters
        {
            K = section.Double("k", defaults.K),
            PriceFloor = section.Money("price_floor", defaults.PriceFloor),
            Rationing = section.Choice(
                "rationing",
                defaults.Rationing.ToTomlValue(),
                EnumeratedParameters.Rationings) switch
            {
                "willingness" => Rationing.Willingness,
                _ => Rationing.Random,
            },
        };

        section.RejectUnknownKeys();
        return prices;
    }

    private static MoneyParameters ReadMoney(TomlSection section, MoneyParameters defaults)
    {
        var money = new MoneyParameters
        {
            OpeningPoolMonths = section.Int("opening_pool_months", defaults.OpeningPoolMonths),
        };

        section.RejectUnknownKeys();
        return money;
    }

    /// <summary>
    /// A column scale, made usable and **snapped**.
    ///
    /// Snapped so that normalising an already-normalised table is a no-op to the bit: the effective
    /// configuration prints the normalised weights, and reloading it must give back the same run
    /// rather than one divided by 1.0000000000000002.
    /// </summary>
    private static double Scale(double scale)
    {
        var usable = scale > 0.0 && double.IsFinite(scale) ? scale : 1.0;

        return Math.Abs(usable - 1.0) < 1e-9 ? 1.0 : usable;
    }

    /// <summary>`round(households / life)`. Defined once, on the schema, so the loader and a configuration built in code cannot disagree.</summary>
    internal static int DerivedCapacity(int households, int life) =>
        SimulationParameters.DerivedCapacity(households, life);

    // ---- ranges and cross-parameter checks ---------------------------------------------------

    private static void Check(SimulationParameters p, Validation problems)
    {
        problems
            .Require(p.Run.Households > 0, "run.households", "a positive count", p.Run.Households)
            .Require(p.Run.Ticks > 0, "run.ticks", "a positive count", p.Run.Ticks)
            .Require(p.Run.Seeds > 0, "run.seeds", "a positive count", p.Run.Seeds)
            .Require(p.Run.WarmupTicks >= 0, "run.warmup_ticks", "zero or more", p.Run.WarmupTicks)
            .Require(Share(p.Run.AbstainerShare), "run.abstainer_share", "a share in [0, 1]", p.Run.AbstainerShare);

        // Warm-up has to leave a measured window behind it, or the run reports nothing.
        problems.Require(
            p.Run.WarmupTicks < p.Run.Ticks,
            "run.warmup_ticks",
            $"fewer than run.ticks ({p.Run.Ticks}), or there is no measured window left",
            p.Run.WarmupTicks);

        problems
            .Require(!p.Income.MeanIncome.IsNegative && !p.Income.MeanIncome.IsZero, "income.mean_income", "more than zero", p.Income.MeanIncome.ToCsv())
            .Require(p.Income.SigmaIncome > 0, "income.sigma_income", "more than zero", p.Income.SigmaIncome)
            .Require(Share(p.Income.OpeningCashShare), "income.opening_cash_share", "a share in [0, 1]", p.Income.OpeningCashShare);

        problems
            .Require(p.Decision.Lambda > 0, "decision.lambda", "more than zero", p.Decision.Lambda)
            .Require(p.Decision.SigmaW > 0, "decision.sigma_w", "more than zero", p.Decision.SigmaW)
            .Require(
                p.Run.Scenario.Length > 0 && p.Run.Scenario.AsSpan().IndexOfAny(",\"\r\n") < 0,
                "run.scenario",
                "a non-empty label with no comma, quote or line break — it is written into every CSV row",
                p.Run.Scenario)
            .Require(Share(p.Decision.SubsistenceShare), "decision.subsistence_share", "a share in [0, 1]", p.Decision.SubsistenceShare)
            .Require(p.Decision.BufferMonths >= 0.0 && double.IsFinite(p.Decision.BufferMonths), "decision.buffer_months", "zero or more months (zero switches the buffer rule off)", p.Decision.BufferMonths);

        problems
            .Require(p.Credit.LoanRate.PercentPerAnnum >= 0, "credit.loan_rate", "zero or more per cent per annum", p.Credit.LoanRate.PercentPerAnnum)
            .Require(Share(p.Credit.ThetaMin), "credit.theta_min", "a share in [0, 1]", p.Credit.ThetaMin)
            .Require(Share(p.Credit.ThetaMax), "credit.theta_max", "a share in [0, 1]", p.Credit.ThetaMax)
            .Require(p.Credit.ThetaMin <= p.Credit.ThetaMax, "credit.theta_min", $"at most credit.theta_max ({Format(p.Credit.ThetaMax)})", p.Credit.ThetaMin);

        problems
            .Require(p.Prices.K > 0 && p.Prices.K <= 1, "prices.k", "an adjustment speed in (0, 1]", p.Prices.K)
            .Require(!p.Prices.PriceFloor.IsNegative && !p.Prices.PriceFloor.IsZero, "prices.price_floor", "more than zero", p.Prices.PriceFloor.ToCsv());

        problems.Require(p.Money.OpeningPoolMonths > 0, "money.opening_pool_months", "a positive count", p.Money.OpeningPoolMonths);

        CheckCategories(p, problems);
        CheckTiers(p, problems);
        CheckArchetypes(p, problems);
        CheckShelves(p, problems);
    }

    /// <summary>
    /// No shelf too thin to carry a price series (11-05).
    ///
    /// A shelf is a (good, tier) pair and each one reprices on its own from its own excess demand.
    /// Below a handful of units that series is not a measurement of anything: a single unit sold or
    /// not sold moves it by the whole reprice step, so it looks exactly like data and is a coin
    /// toss. The eighteen-good table makes this pressing rather than theoretical — splitting a
    /// category into three divides its capacity three ways as well as its budget — and at 1,000
    /// households it puts four premium shelves at three units or fewer, one of them at **one**.
    ///
    /// **The counts come from <see cref="Allocation.LargestRemainder"/>, not from
    /// `round(share · capacity)`, because the two disagree.** A capacity of 19 splits 8 / 7 / 4,
    /// where rounding each share on its own gives 8 / 8 / 4 and a shelf that does not exist. Using
    /// the wrong one here would make the check disagree with the table it is checking, in the
    /// direction of passing.
    ///
    /// Every offending shelf is named, not just the first: the fix is more households or a group
    /// merged back, and neither follows from one count.
    /// </summary>
    private static void CheckShelves(SimulationParameters p, Validation problems)
    {
        var floor = p.Run.MinShelfUnits;

        problems.Require(floor >= 0, "run.min_shelf_units", "zero or more (zero switches the check off)", floor);

        if (floor <= 0 || p.Tiers.Count == 0)
        {
            return;
        }

        var shares = p.Tiers.Select(t => t.UnitShare).ToArray();

        if (shares.Any(share => share < 0.0 || double.IsNaN(share)) || shares.Sum() <= 0.0)
        {
            // CheckTiers is already saying so, and an allocation on those weights would throw.
            return;
        }

        var thin = new List<string>();

        foreach (var good in p.Categories)
        {
            if (good.Capacity <= 0)
            {
                continue;
            }

            var perTier = Allocation.LargestRemainder(good.Capacity, shares);

            for (var t = 0; t < p.Tiers.Count; t++)
            {
                if (perTier[t] < floor)
                {
                    thin.Add(Invariant($"{good.Name}.{p.Tiers[t].Name} = {perTier[t]}"));
                }
            }
        }

        problems.Require(
            thin.Count == 0,
            "the opening shelves",
            Invariant($"every (good, tier) shelf at {floor} units or more — below that a shelf's ")
            + "price series is a coin toss that looks like data. Raise run.households, or merge a "
            + "group back into its neighbour",
            string.Join(", ", thin));
    }

    /// <summary>
    /// The table has to be a population, and every exponent has to leave the ladder a ladder.
    /// </summary>
    private static void CheckArchetypes(SimulationParameters p, Validation problems)
    {
        var archetypes = p.Archetypes;

        problems.Require(
            archetypes.SigmaIdio >= 0.0 && double.IsFinite(archetypes.SigmaIdio),
            "archetypes.sigma_idio",
            "zero or more (zero switches the residual off, which is v1)",
            archetypes.SigmaIdio);

        if (archetypes.Types.Count == 0)
        {
            // Already reported by Normalise; one complaint per mistake.
            return;
        }

        foreach (var type in archetypes.Types)
        {
            problems.Require(
                type.Share > 0.0 && type.Share <= 1.0,
                $"archetypes.{type.Name}.share",
                "a share in (0, 1] — a type nobody is cannot be assigned to anybody",
                type.Share);
        }

        var shares = archetypes.Types.Sum(a => a.Share);

        problems.Require(
            Math.Abs(shares - 1.0) < 1e-9,
            "archetypes.share",
            "shares summing to exactly 1 — they split the population",
            Format(shares));

        CheckCycles(p, problems);

        // The bound is read off the tier table, never written down: a literal would be correct
        // today and silently wrong the first time somebody edits a tier multiplier.
        var maxExponent = QualityLadder.MaxExponent(p.Tiers);

        if (double.IsNaN(maxExponent))
        {
            // The tier ladder is already broken and CheckTiers is saying so. A bound derived from
            // it would be a second, confusing complaint about the same mistake.
            return;
        }

        foreach (var type in archetypes.Types)
        {
            foreach (var exponent in type.Kappa)
            {
                problems.Require(
                    exponent.Value > 0.0 && exponent.Value < maxExponent,
                    $"archetypes.{type.Name}.kappa.{exponent.Category}",
                    $"a steepness in (0, {Format(maxExponent)}) — above that this tier table's "
                    + "upgrade ladder inverts and buying the budget unit scores below upgrading it",
                    exponent.Value);
            }
        }
    }

    /// <summary>
    /// What a replacement-cycle multiplier needs from the rest of the configuration (§3.7).
    ///
    /// Per **good** rather than per (type, good), because normalisation is per column: the moment
    /// one type states a `d`, every other type's 1.0 is scaled off 1 as well, and complaining about
    /// each of them separately would report one mistake four times.
    ///
    /// The three conditions are all cases of the same thing — a `d` that cannot mean what it says:
    /// under `deterministic` a life is an integer count of ticks and a fractional one would have to
    /// be rounded, which breaks the identity by more than an arithmetic normalisation would; a
    /// life-1 good is consumed the tick it is bought and has no cycle to stretch; and a realised
    /// life below one tick turns `1 / life` into something that is not a probability, so the good
    /// silently becomes a consumable.
    /// </summary>
    private static void CheckCycles(SimulationParameters p, Validation problems)
    {
        var hazard = p.Run.Replacement == Replacement.Hazard;

        foreach (var good in p.Categories)
        {
            var column = p.Archetypes.Types
                .Select(t => (t.Name, Cycle: t.CycleFor(good.Name)))
                .ToArray();

            if (column.All(c => c.Cycle == 1.0))
            {
                continue;
            }

            var stated = string.Join(", ", column.Select(c => $"{c.Name} = {Format(c.Cycle)}"));

            problems.Require(
                hazard,
                $"archetypes.d.{good.Name}",
                "run.replacement = \"hazard\" — a deterministic life is an integer count of ticks, "
                + "so a cycle multiplier would have to be rounded away rather than applied",
                stated);

            problems.Require(
                good.Life > 1,
                $"archetypes.d.{good.Name}",
                $"a good with a life to stretch — {good.Name} has life 1 and is consumed the tick "
                + "it is bought, so a cycle multiplier has nothing to multiply",
                stated);

            var shortest = column.Min(c => c.Cycle) * good.Life;

            problems.Require(
                shortest >= 1.0,
                $"archetypes.d.{good.Name}",
                "a realised life of at least one tick for every type — the per-tick failure "
                + "probability is 1 / life, and below one tick that stops being a probability",
                Format(shortest));
        }
    }

    private static void CheckCategories(SimulationParameters p, Validation problems)
    {
        problems.Require(p.Categories.Count > 0, "categories", "at least one category", p.Categories.Count);

        foreach (var duplicate in p.Categories.GroupBy(c => c.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            problems.Fail($"categories.{duplicate.Key}", "one definition", $"{duplicate.Count()} definitions");
        }

        foreach (var category in p.Categories)
        {
            var key = $"categories.{category.Name}";

            problems
                .Require(category.Life >= 1, $"{key}.life", "at least 1 tick", category.Life)
                .Require(category.Capacity > 0, $"{key}.capacity", "a positive count", category.Capacity)
                .Require(!category.PriceRef.IsNegative && !category.PriceRef.IsZero, $"{key}.price_ref", "more than zero", category.PriceRef.ToCsv())
                .Require(category.V > 0, $"{key}.v", "more than zero", category.V)
                .Require(Share(category.Necessity), $"{key}.necessity", "a share in [0, 1]", category.Necessity);

            problems.Require(
                category.BaseScore >= 0.0 && double.IsFinite(category.BaseScore),
                $"{key}.base_score",
                "zero or more (zero means this row states v directly)",
                category.BaseScore);

            // `v` is a definition wherever a base score is authored, and the rule is capacity's:
            // omit it and it is computed, state it and it has to be right. The tolerance is
            // relative and tiny — this catches a number somebody rounded, not a number somebody
            // typed a digit wrong in, and both are the same mistake for these purposes.
            if (category.BaseScore > 0.0 && category.Life >= 1 && !p.Income.MeanIncome.IsZero)
            {
                var derived = category.DerivedV(p.Income.MeanIncome);

                problems.Require(
                    Math.Abs(category.V - derived) <= 1e-9 * Math.Max(derived, 1e-9),
                    $"{key}.v",
                    $"base_score × price_ref / (life × mean_income) = {derived.ToString("R", CultureInfo.InvariantCulture)} — omit it and it is computed",
                    Format(category.V));
            }

            // capacity is a definition, not a choice: round(households / life).
            if (category.Life >= 1 && p.Run.Households > 0)
            {
                var derived = DerivedCapacity(p.Run.Households, category.Life);

                problems.Require(
                    category.Capacity == derived,
                    $"{key}.capacity",
                    $"round(households / life) = {derived}, the steady-state replacement demand — "
                    + "omit it and it is computed",
                    category.Capacity);
            }

            if (category.Financeable)
            {
                problems.Require(category.Term >= 1, $"{key}.term", "at least 1 month for a financeable category", category.Term);
            }
            else
            {
                problems.Require(category.Term == 0, $"{key}.term", "0 for a category that cannot be financed", category.Term);
            }
        }
    }

    private static void CheckTiers(SimulationParameters p, Validation problems)
    {
        if (p.Tiers.Count < 2)
        {
            problems.Fail("tiers", "at least two tiers, so that there is a ladder to climb", p.Tiers.Count);
            return;
        }

        foreach (var tier in p.Tiers)
        {
            var key = $"tiers.{tier.Name}";

            problems
                .Require(tier.PriceMult > 0, $"{key}.price_mult", "more than zero", tier.PriceMult)
                .Require(tier.ValueMult > 0, $"{key}.value_mult", "more than zero", tier.ValueMult)
                .Require(Share(tier.UnitShare), $"{key}.unit_share", "a share in [0, 1]", tier.UnitShare);
        }

        // The unit shares split a category's capacity, so they have to be a split.
        var shares = p.Tiers.Sum(t => t.UnitShare);
        problems.Require(
            Math.Abs(shares - 1.0) < 1e-9,
            "tiers.unit_share",
            "shares summing to exactly 1 — they split a category's capacity",
            Format(shares));

        // The ladder has to be a ladder: each tier dearer and better than the one below.
        for (var i = 1; i < p.Tiers.Count; i++)
        {
            var below = p.Tiers[i - 1];
            var tier = p.Tiers[i];

            problems
                .Require(tier.PriceMult > below.PriceMult, $"tiers.{tier.Name}.price_mult", $"more than tiers.{below.Name} ({Format(below.PriceMult)})", tier.PriceMult)
                .Require(tier.ValueMult > below.ValueMult, $"tiers.{tier.Name}.value_mult", $"more than tiers.{below.Name} ({Format(below.ValueMult)}) — a step up must be worth having", tier.ValueMult);
        }

        CheckDiminishingReturns(p, problems);
    }

    /// <summary>
    /// Value for money has to fall at every step up the ladder.
    ///
    /// This is `02-PARAMETERS.md` §3.2 — `value_mult` rises more slowly than `price_mult` — in the
    /// form that can be checked. Set a premium tier that is better value than standard and the
    /// ladder inverts: every household jumps straight to premium, and the run produces numbers
    /// that look entirely fine. One line, against a failure that would take a day to find.
    /// </summary>
    private static void CheckDiminishingReturns(SimulationParameters p, Validation problems)
    {
        var previous = double.PositiveInfinity;
        var previousTier = "nothing";

        // The same step ratios QualityLadder.MaxExponent walks, at kappa = 1. One definition of
        // value-for-money, two readers: this one says which step went wrong, that one finds the
        // exponent at which the ordering breaks.
        var ratios = QualityLadder.StepRatios(p.Tiers, 1.0);

        for (var i = 0; i < p.Tiers.Count; i++)
        {
            var tier = p.Tiers[i];
            var ratio = ratios[i];

            if (double.IsNaN(ratio))
            {
                // A step that costs nothing: already reported by the ladder check above.
                return;
            }

            problems.Require(
                ratio < previous,
                $"tiers.{tier.Name}",
                $"value for money below the step before it ({previousTier}, {Format(previous)}) — "
                + "quality must have diminishing returns, or every household jumps to the top tier "
                + "and the run still looks plausible",
                Format(ratio));

            previous = ratio;
            previousTier = tier.Name;
        }
    }

    private static bool Share(double value) => value is >= 0.0 and <= 1.0;

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
