using System.Globalization;
using FluentResults;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

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

    public static Result<SimulationParameters> FromToml(string toml)
    {
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

            return syntax.ToResult<SimulationParameters>(() => SimulationParameters.Default);
        }

        var problems = new Validation();
        var defaults = SimulationParameters.Default;

        var run = ReadRun(Section(root, "run", problems), defaults.Run);
        var income = ReadIncome(Section(root, "income", problems), defaults.Income);
        var categories = ReadCategories(Section(root, "categories", problems), run, problems);
        var tiers = ReadTiers(Section(root, "tiers", problems), problems);
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
        ["run", "income", "categories", "tiers", "decision", "credit", "prices", "money"];

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
    /// The goods table, as an **overlay** on the specification's six categories.
    ///
    /// A `[categories.food]` table changes food and leaves the other five alone. It does not
    /// replace the table — a file that mentioned one category and silently got a one-category
    /// economy would run, and produce a number, and look like the run that was asked for.
    ///
    /// A name the specification does not know is a new category, and then every field is the
    /// file's responsibility. That is how a seventh category is added without touching the engine.
    /// </summary>
    private static IReadOnlyList<CategoryParameters> ReadCategories(
        TomlSection section,
        RunParameters run,
        Validation problems)
    {
        var stated = section.Subtables();
        var categories = new List<CategoryParameters>();

        foreach (var name in SimulationParameters.Default.Categories.Select(c => c.Name).Concat(stated).Distinct(StringComparer.Ordinal))
        {
            var known = SimulationParameters.Default.Categories.FirstOrDefault(c => c.Name == name);
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

            var category = new CategoryParameters
            {
                Name = name,
                Life = life,
                // capacity is round(households / life) by definition, so it is derived unless the
                // file states it — and if it does, Check makes sure it states the right value.
                Capacity = row.Int("capacity", DerivedCapacity(run.Households, life)),
                PriceRef = row.Money("price_ref", known?.PriceRef ?? Engine.Money.Zero),
                V = row.Double("v", known?.V ?? 0.0),
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
    private static IReadOnlyList<TierParameters> ReadTiers(TomlSection section, Validation problems)
    {
        var stated = section.Subtables();
        var tiers = new List<TierParameters>();

        foreach (var name in SimulationParameters.Default.Tiers.Select(t => t.Name).Concat(stated).Distinct(StringComparer.Ordinal))
        {
            var known = SimulationParameters.Default.Tiers.FirstOrDefault(t => t.Name == name);
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

    internal static int DerivedCapacity(int households, int life) =>
        life < 1 ? 0 : (int)Math.Round((double)households / life, MidpointRounding.AwayFromZero);

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

        for (var i = 0; i < p.Tiers.Count; i++)
        {
            var tier = p.Tiers[i];

            var deltaValue = i == 0 ? tier.ValueMult : tier.ValueMult - p.Tiers[i - 1].ValueMult;
            var deltaPrice = i == 0 ? tier.PriceMult : tier.PriceMult - p.Tiers[i - 1].PriceMult;

            if (deltaPrice <= 0)
            {
                // Already reported by the ladder check above.
                return;
            }

            var ratio = deltaValue / deltaPrice;

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
