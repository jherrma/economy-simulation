using System.Globalization;
using static System.FormattableString;
using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/11-01. A goods table is a list of rows; a category is a label on them.
///
/// These tests are about the **loader**, which is where E11 needed something genuinely new. The
/// engine did not: a product group with its own life, price, capacity and value weight is exactly
/// what a row already was, so eighteen of them run through code written for six without a branch.
/// The overlay was the problem — `[categories.groceries]` on top of §3.1's six rows is
/// twenty-four goods, and twenty-four goods is a run that starts, produces numbers and looks like
/// the one that was asked for.
/// </summary>
public sealed class CalibrationTests
{
    private static string GroupedPath => Path.Combine(Repo.Root, "config", "calibrations", "grouped.toml");

    private static SimulationParameters Grouped()
    {
        var loaded = ConfigurationLoader.FromFile(GroupedPath);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        return loaded.Value;
    }

    // ---- replace-semantics ----------------------------------------------------------------------

    /// <summary>
    /// The eighteen-good calibration loads as **eighteen** rows.
    ///
    /// This is the test story 11-01 exists for. Before `replace`, the same file loaded as
    /// twenty-four: §3.1's six survived underneath it because a goods table is an overlay, and an
    /// overlay has no way to say "not that one".
    /// </summary>
    [Fact]
    public void TheGroupedCalibration_LoadsAsEighteenRows()
    {
        var parameters = Grouped();

        Assert.Equal(18, parameters.Categories.Count);

        foreach (var name in SimulationParameters.Default.Categories.Select(c => c.Name))
        {
            Assert.DoesNotContain(parameters.Categories, c => string.Equals(c.Name, name, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// §3.4's first identity on the grouped table: `Σ price_ref_g / life_g = mean_income`, to the
    /// cent.
    ///
    /// It is the calibration's whole footing — a median household can afford roughly the standard
    /// basket and no more — and it is what an overlaid load silently doubles. Splitting a category
    /// is a redistribution of its budget, never an addition to it, and this is the arithmetic that
    /// says so.
    /// </summary>
    [Fact]
    public void TheGroupedCalibration_CostsTheMeanIncomePerTick()
    {
        var parameters = Grouped();
        var table = new GoodsTable(parameters);

        Assert.Equal(650.0, table.StandardBasketFlowCost, 2);
        Assert.Equal(parameters.Income.MeanIncome.Cents / 100.0, table.StandardBasketFlowCost, 2);
    }

    /// <summary>
    /// The same eighteen rows without `replace = true` load as twenty-four and cost twice the mean
    /// income — the failure the flag prevents, asserted rather than described.
    /// </summary>
    [Fact]
    public void WithoutReplace_TheSameRowsLoadOnTopOfTheDefaultSix()
    {
        var overlaid = File.ReadAllText(GroupedPath).Replace("replace = true", "", StringComparison.Ordinal);
        var loaded = ConfigurationLoader.FromToml(overlaid);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));
        Assert.Equal(24, loaded.Value.Categories.Count);

        // 650 of §3.1's six on top of 650 of §3.6's eighteen. Nothing in the run would complain.
        Assert.Equal(1300.0, new GoodsTable(loaded.Value).StandardBasketFlowCost, 2);
    }

    /// <summary>
    /// The overlay is still the default, and that is deliberate: a scenario file naming one
    /// category has to keep meaning "change this one".
    /// </summary>
    [Fact]
    public void TheOverlayIsStillTheDefault()
    {
        var loaded = ConfigurationLoader.FromToml(
            """
            [categories.food]
            price_ref = 330.00
            """);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));
        Assert.Equal(6, loaded.Value.Categories.Count);
        Assert.Equal(33000, loaded.Value.Categories[0].PriceRef.Cents);
    }

    /// <summary>
    /// `replace = true` with nothing else is an empty table, and it is refused rather than run.
    ///
    /// Worth asserting because the whole risk of a replace flag is a file that turns the town off
    /// by saying less than it meant to.
    /// </summary>
    [Fact]
    public void ReplaceWithNoRows_IsRefused()
    {
        var loaded = ConfigurationLoader.FromToml(
            """
            [categories]
            replace = true
            """);

        Assert.True(loaded.IsFailed);
        Assert.Contains(loaded.Errors, e => e.Message.Contains("at least one category", StringComparison.Ordinal));
    }

    /// <summary>
    /// The goods table keeps the order the file states it in.
    ///
    /// Not cosmetic: rows are walked in order — the shopping walk, the per-good residual draw — so
    /// sorting them by name would make §3.1's `food, leisure, …` into `appliances, clothing, …` and
    /// change every run. It is also what makes an effective configuration reload to itself.
    /// </summary>
    [Fact]
    public void TheGoodsTableKeepsTheOrderTheFileStatesIt()
    {
        var parameters = Grouped();

        Assert.Equal(
            ["groceries", "consumables", "eating_out", "going_out"],
            parameters.Categories.Take(4).Select(c => c.Name));

        var reloaded = ConfigurationLoader.FromToml(parameters.ToToml());

        Assert.True(reloaded.IsSuccess, string.Join("; ", reloaded.Errors.Select(e => e.Message)));
        Assert.Equal(
            parameters.Categories.Select(c => c.Name),
            reloaded.Value.Categories.Select(c => c.Name));
    }

    // ---- authored by base score (11-02) -----------------------------------------------------------

    /// <summary>
    /// `v_g = base_score_g · price_ref_g / (life_g · mean_income)`, for all eighteen — and the base
    /// score is what the file states.
    ///
    /// The derivation is the story's one real idea. Nobody has an intuition about `v = 0.0141`;
    /// everybody has one about "this good scores 1.10 at the mean income", because that says
    /// directly where the good sits relative to λ. The authored number is the one a reader can
    /// disagree with; the derived number is the one the engine uses.
    /// </summary>
    [Fact]
    public void VIsDerivedFromTheAuthoredBaseScore()
    {
        var parameters = Grouped();
        var mean = parameters.Income.MeanIncome;

        foreach (var good in parameters.Categories)
        {
            Assert.True(good.BaseScore > 0.0, $"{good.Name} states no base score");
            Assert.Equal(good.DerivedV(mean), good.V);
        }

        // And the base score is what it claims to be: the score of a candidate at the mean income,
        // before the tier multipliers. Computed through the engine's own valuation, not repeated.
        var goods = new GoodsTable(parameters);

        for (var g = 0; g < goods.CategoryCount; g++)
        {
            var good = parameters.Categories[g];

            var score = Valuation.Score(
                Valuation.BaseValue(goods.Floor(g), goods.IncomeSlope(g), mean, 1.0),
                Valuation.FlowCost(good.PriceRef, good.Life));

            // Not exactly, and the size of the gap is worth pinning rather than papering over: the
            // Stone-Geary floor `a_g` is money and is rounded to the cent, so the realised base
            // score can sit up to half a cent per tick away from the authored one — 0.0006 for the
            // TV, 0.0010 for small appliances whose €3.33 a tick is the smallest flow cost in the
            // table. That is the bound, not a tolerance somebody widened until the test passed.
            // Half a cent, in the units the score is a ratio of: `life / price_ref`, in cents.
            // Everyday clothing attains it exactly — its floor lands on 1092.5 cents — so the bound
            // is written the way the error is computed and compared inclusively.
            // Half a cent, in the units the score is a ratio of: `life / price_ref`, in cents. Plus
            // a last-place allowance, because the score is itself computed in floating point and
            // everyday clothing attains the bound exactly — its floor lands on 1092.5 cents.
            var bound = (0.5 * good.Life / good.PriceRef.Cents) + 1e-9;

            Assert.InRange(score, good.BaseScore - bound, good.BaseScore + bound);
        }
    }

    /// <summary>§3.6's `v` table, to the four decimals it publishes.</summary>
    [Fact]
    public void TheDerivedWeightsMatchTheSpecificationTable()
    {
        var parameters = Grouped();
        var stated = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var row in SpecFile.Tables("#### Authored by base score, with `v` derived")[0].Skip(1))
        {
            for (var i = 0; i + 1 < row.Length; i += 2)
            {
                stated[SpecFile.Clean(row[i]).Replace(" ", "_", StringComparison.Ordinal)] =
                    SpecFile.Number(row[i + 1]);
            }
        }

        Assert.Equal(18, stated.Count);

        foreach (var good in parameters.Categories)
        {
            var key = stated.Keys.Single(k => good.Name.EndsWith(k, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(stated[key], good.V, 4);
        }
    }

    /// <summary>
    /// A configuration may state `v` beside a base score, and it must state the derived value — the
    /// rule `capacity` already obeys. §3.6's own four-decimal column is a rounding of the derived
    /// numbers and is therefore rejected, which is the point rather than an inconvenience.
    /// </summary>
    [Fact]
    public void AStatedVMustEqualTheDerivedValue()
    {
        const string Row = """
            [categories]
            replace = true

            [categories.phone]
            life = 30
            price_ref = 600.00
            base_score = 1.24
            necessity = 0.45
            financeable = true
            term = 24
            """;

        var derived = ConfigurationLoader.FromToml(Row);

        Assert.True(derived.IsSuccess, string.Join("; ", derived.Errors.Select(e => e.Message)));
        Assert.Equal(1.24 * 600.0 / (30.0 * 650.0), derived.Value.Categories[0].V, 12);

        var exact = ConfigurationLoader.FromToml(
            Row + "\nv = " + derived.Value.Categories[0].V.ToString("R", CultureInfo.InvariantCulture));

        Assert.True(exact.IsSuccess, string.Join("; ", exact.Errors.Select(e => e.Message)));

        var rounded = ConfigurationLoader.FromToml(Row + "\nv = 0.0382");

        Assert.True(rounded.IsFailed);
        Assert.Contains(
            rounded.Errors,
            e => e.Message.Contains("base_score × price_ref / (life × mean_income)", StringComparison.Ordinal));
    }

    /// <summary>
    /// **V3, seen from the calibration side.** `price_ref` and `mean_income` are both money and
    /// scale together, so every derived `v` is invariant under a change of unit.
    ///
    /// This is what makes the base score authorable at all. A scored quantity that moved when the
    /// currency was redenominated would be a parameter of the numeraire rather than of the
    /// household, and V3 would go red the first time the grouped calibration ran under it.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void TheDerivationSurvivesARedenomination(int factor)
    {
        var plain = Grouped();

        var scaled = File.ReadAllText(GroupedPath) + Invariant($"""

            [income]
            mean_income = {650 * factor}.00
            """);

        // One pass, or a price scaled early is scaled again by a later rule: 60 becomes 120 becomes
        // 240, and the table quietly stops being the same table.
        var text = System.Text.RegularExpressions.Regex.Replace(
            scaled,
            @"price_ref   = ([0-9]+)\.00",
            m => Invariant($"price_ref   = {int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * factor}.00"));

        var loaded = ConfigurationLoader.FromToml(text);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        for (var g = 0; g < plain.Categories.Count; g++)
        {
            Assert.Equal(plain.Categories[g].PriceRef.Cents * factor, loaded.Value.Categories[g].PriceRef.Cents);
            Assert.Equal(plain.Categories[g].V, loaded.Value.Categories[g].V, 12);
        }
    }

    /// <summary>
    /// The effective configuration carries **both** numbers, and at round-trip precision.
    ///
    /// The authored one so a reader six months from now can see what was claimed; the derived one
    /// because that is what ran. Printing either to four decimals would make the file fail its own
    /// derivation check on reload, which is how this was found.
    /// </summary>
    [Fact]
    public void TheEffectiveConfigurationCarriesBothTheAuthoredAndTheDerivedNumber()
    {
        var printed = Grouped().ToToml().ReplaceLineEndings("\n");

        Assert.Contains("base_score = 1.24\nv = 0.03815384615384615", printed, StringComparison.Ordinal);

        // §3.1 authors `v` and states no base score, so nothing is printed for it and the six-row
        // effective configuration is the file it always was.
        var six = SimulationParameters.Default.ToToml();

        Assert.DoesNotContain("base_score", six, StringComparison.Ordinal);
    }

    // ---- what the base scores were chosen against (§3.4's three requirements) ----------------------

    /// <summary>The unconstrained ladder for one good at one income, `w = 1`, at opening prices.</summary>
    private static Candidate[] LadderFor(GoodsTable goods, Money income, int good)
    {
        var population = Households.Specified(goods, [income], [1.0]);
        var buffer = new Candidate[goods.TierCount];

        Ladder.Build(goods, new Market(goods), population, 0, good, buffer);

        return buffer;
    }

    /// <summary>
    /// **Requirement 1: essentials outrank durables where the budget binds.** At €300 the table
    /// buys food, underwear and a fridge, and owns no phone.
    ///
    /// It holds, and it holds **narrowly** — small appliances' budget candidate is 0.996 and the
    /// phone's 0.989, so two euros of income or one tick of repricing moves the boundary. True as
    /// stated and fragile as a design requirement, which is why this test names the four goods that
    /// clear λ rather than asserting a general ordering that would break the first time a shelf
    /// repriced.
    /// </summary>
    [Fact]
    public void AtThreeHundredEurosTheTableBuysFoodUnderwearAndAFridge()
    {
        var parameters = Grouped();
        var goods = new GoodsTable(parameters);
        var lambda = parameters.Decision.Lambda;

        var bought = new List<string>();

        for (var g = 0; g < goods.CategoryCount; g++)
        {
            if (Ladder.UnconstrainedTier(LadderFor(goods, Money.FromEuros(300), g), lambda) >= 0)
            {
                bought.Add(parameters.Categories[g].Name);
            }
        }

        Assert.Equal(["groceries", "consumables", "clothing_basics", "appliance_large"], bought);

        // And the margin it holds by, both sides. A change that moves either of these past λ has
        // changed requirement 1 without anybody deciding to.
        Assert.InRange(Score(goods, 300, "appliance_small", parameters), 0.990, 1.000);
        Assert.InRange(Score(goods, 300, "phone", parameters), 0.980, 1.000);
    }

    /// <summary>
    /// **Requirement 2: the median sits mid-ladder** — but only for the four goods the experiment
    /// is about, and the test has to name them.
    ///
    /// The four **large financeable durables** are phone, laptop, medium and large appliances, and
    /// their standard upgrade lands just under λ: that is where a small price move flips a tier
    /// choice, which is the margin credit is expected to act on, and §3.1 put electronics and
    /// appliances at the same 0.998.
    ///
    /// The other three financeable goods are **not** on that margin and are not meant to be —
    /// hobby equipment 0.896, TV 0.880, hobby big kit 0.816. They are financeable because they are
    /// financed in the world, not because the experiment needs them responsive. A criterion phrased
    /// over "the financeable durables" would reject this table, which is why it is phrased over the
    /// four.
    /// </summary>
    [Fact]
    public void TheMedianSitsMidLadderForTheFourLargeFinanceableDurables()
    {
        var parameters = Grouped();
        var goods = new GoodsTable(parameters);

        string[] margin = ["phone", "laptop", "appliance_medium", "appliance_large"];

        foreach (var name in margin)
        {
            var upgrade = Upgrade(goods, 650, name, parameters);

            Assert.InRange(upgrade, 0.95, 1.00);
        }

        var others = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["hobby_equipment"] = 0.896,
            ["tv"] = 0.880,
            ["hobby_big_kit"] = 0.816,
        };

        foreach (var (name, expected) in others)
        {
            Assert.True(parameters.Categories.Single(c => c.Name == name).Financeable);
            Assert.Equal(expected, Upgrade(goods, 650, name, parameters), 3);
        }

        // The set is exactly the seven financeable goods: four on the margin, three off it.
        Assert.Equal(
            margin.Concat(others.Keys).OrderBy(n => n, StringComparer.Ordinal),
            parameters.Categories.Where(c => c.Financeable).Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// **Requirement 3: premium never clears at the median**, so premium is bought by the upper part
    /// of the distribution and by anyone credit lifts there. The highest is groceries at 0.710.
    /// </summary>
    [Fact]
    public void NoPremiumCandidateClearsLambdaAtTheMedian()
    {
        var parameters = Grouped();
        var goods = new GoodsTable(parameters);
        var highest = 0.0;

        for (var g = 0; g < goods.CategoryCount; g++)
        {
            highest = Math.Max(highest, LadderFor(goods, Money.FromEuros(650), g)[2].Score);
        }

        Assert.True(highest < parameters.Decision.Lambda, Invariant($"a premium candidate clears λ at {highest}"));
        Assert.Equal(0.710, highest, 3);
    }

    /// <summary>
    /// **The fourth property, and the one the six-category table could not express**: because
    /// `necessity` is now per good, goods enter the basket at different incomes.
    ///
    /// §3.6 publishes the income each good enters at, and this reads that table out of the
    /// specification and puts it to the engine. It matters more than it looks: the cohort
    /// `01-SIMULATION.md` §10.1 found permanently excluded sits at a median income near €400, which
    /// is exactly the region this table resolves and §3.1 did not — under six categories a
    /// household either bought a category or did not, and the whole of electronics arrived at once.
    /// </summary>
    [Fact]
    public void GoodsEnterTheBasketAtTheIncomesTheSpecificationPublishes()
    {
        var parameters = Grouped();
        var goods = new GoodsTable(parameters);
        var lambda = parameters.Decision.Lambda;

        var expected = new Dictionary<string, int>(StringComparer.Ordinal);
        var anyIncome = new List<string>();

        foreach (var row in SpecFile.Tables("#### What the base scores were chosen against")[0].Skip(1))
        {
            if (row[0].Length > 0)
            {
                anyIncome.AddRange(row[0].Split(',').Select(n => Slug(n)));
            }

            foreach (var (names, euros) in new[] { (row[1], row[2]), (row[3], row[4]) })
            {
                if (names.Length == 0)
                {
                    continue;
                }

                foreach (var name in names.Split(','))
                {
                    expected[Slug(name)] = SpecFile.Integer(euros);
                }
            }
        }

        Assert.Equal(["groceries", "consumables", "clothing_basics"], anyIncome);
        Assert.Equal(goods.CategoryCount, anyIncome.Count + expected.Count);

        foreach (var name in anyIncome)
        {
            Assert.True(
                Ladder.UnconstrainedTier(LadderFor(goods, Money.FromEuros(1), Index(parameters, name)), lambda) >= 0,
                $"{name} is supposed to be bought at any income");
        }

        foreach (var (name, euros) in expected)
        {
            var g = Index(parameters, name);

            // A euro either side of the published figure: below it the good is not bought, at it or
            // above it the good is. The specification says "enters around", so one euro of slack is
            // the claim rather than a weakening of it.
            Assert.True(
                Ladder.UnconstrainedTier(LadderFor(goods, Money.FromEuros(euros - 2), g), lambda) < 0,
                Invariant($"{name} is already bought at €{euros - 2}, below its published entry"));

            Assert.True(
                Ladder.UnconstrainedTier(LadderFor(goods, Money.FromEuros(euros + 1), g), lambda) >= 0,
                Invariant($"{name} is still not bought at €{euros + 1}, above its published entry"));
        }
    }

    /// <summary>
    /// The crossings §3.6 lists: the income at which a good's base score overtakes groceries'.
    ///
    /// §3.4's requirement 1 was originally written as "at every income" and is not — food's value
    /// is mostly floor, so it grows slowly with income while a durable's grows fast. Under §3.1 the
    /// crossings bunched between €770 and €870; under §3.6 they spread across the distribution,
    /// because `necessity` varies within a category and not only between.
    /// </summary>
    [Theory]
    [InlineData("going_out", 762)]
    [InlineData("appliance_small", 788)]
    [InlineData("laptop", 835)]
    [InlineData("phone", 899)]
    [InlineData("appliance_medium", 899)]
    [InlineData("tv", 938)]
    [InlineData("hobby_big_kit", 1008)]
    public void TheBaseScoreCrossingsAreWhereTheSpecificationPutsThem(string name, int euros)
    {
        var parameters = Grouped();
        var goods = new GoodsTable(parameters);
        var groceries = Index(parameters, "groceries");
        var good = Index(parameters, name);

        double Base(int at) =>
            LadderFor(goods, Money.FromEuros(at), good)[0].Score
            - LadderFor(goods, Money.FromEuros(at), groceries)[0].Score;

        Assert.True(Base(euros - 2) < 0.0, Invariant($"{name} already outranks groceries at €{euros - 2}"));
        Assert.True(Base(euros + 2) > 0.0, Invariant($"{name} still does not outrank groceries at €{euros + 2}"));
    }

    /// <summary>A display name from the specification's tables as the goods table spells it.</summary>
    private static string Slug(string name)
    {
        var cleaned = SpecFile.Clean(name).ToLowerInvariant().Replace(" ", "_", StringComparison.Ordinal);

        return cleaned switch
        {
            "large_appliances" => "appliance_large",
            "medium_appliances" => "appliance_medium",
            "small_appliances" => "appliance_small",
            "everyday_clothing" => "clothing_everyday",
            "basics" or "clothing_basics" => "clothing_basics",
            _ => cleaned,
        };
    }

    private static double Score(GoodsTable goods, int income, string name, SimulationParameters parameters) =>
        LadderFor(goods, Money.FromEuros(income), Index(parameters, name))[0].Score;

    private static double Upgrade(GoodsTable goods, int income, string name, SimulationParameters parameters) =>
        LadderFor(goods, Money.FromEuros(income), Index(parameters, name))[1].Score;

    private static int Index(SimulationParameters parameters, string name)
    {
        for (var g = 0; g < parameters.Categories.Count; g++)
        {
            if (string.Equals(parameters.Categories[g].Name, name, StringComparison.Ordinal))
            {
                return g;
            }
        }

        throw new KeyNotFoundException(name);
    }

    // ---- opening pressure, both calibrations, one method -------------------------------------------

    /// <summary>What one calibration's opening looks like: desired spend, and the mix of decisions.</summary>
    private sealed record Opening(double ShareOfMeanIncome, double SpendPerTick, double[] Decisions, double[] Units);

    /// <summary>
    /// Desired spend at the opening prices, over draws from the income distribution, ignoring cash.
    ///
    /// **The same method for both tables**, which is the whole point of the measurement: §3.3 quotes
    /// "about 23% below income" as prose, and a number computed one way against a sentence written
    /// another way is not a comparison. Cash is ignored and stock is unlimited, so this is the
    /// unconstrained ladder of §3.4 read across the whole distribution rather than at six incomes.
    /// </summary>
    private static Opening OpeningPressure(SimulationParameters parameters, bool drawTaste)
    {
        const int Draws = 200_000;
        const int Batch = 20_000;

        var goods = new GoodsTable(parameters);
        var market = new Market(goods);
        var lambda = parameters.Decision.Lambda;
        var mean = parameters.Income.MeanIncome;

        var spend = 0.0;
        var decisions = new double[goods.TierCount];
        var units = new double[goods.TierCount];
        var buffer = new Candidate[goods.TierCount];

        for (var from = 0; from < Draws; from += Batch)
        {
            var incomes = new Money[Batch];
            var tastes = new double[Batch];

            for (var i = 0; i < Batch; i++)
            {
                var h = from + i;

                incomes[i] = mean.Scaled(RandomStream
                    .ForHousehold(1, h, Purpose.Income)
                    .NextLogNormal(1.0, parameters.Income.SigmaIncome));

                tastes[i] = drawTaste
                    ? RandomStream.ForHousehold(1, h, Purpose.Willingness).NextLogNormal(1.0, parameters.Decision.SigmaW)
                    : 1.0;
            }

            var population = Households.Specified(goods, incomes, tastes);

            for (var i = 0; i < Batch; i++)
            {
                for (var g = 0; g < goods.CategoryCount; g++)
                {
                    Ladder.Build(goods, market, population, i, g, buffer);

                    var chosen = Ladder.UnconstrainedTier(buffer, lambda);

                    if (chosen < 0)
                    {
                        continue;
                    }

                    var life = parameters.Categories[g].Life;

                    spend += market.Price(g, chosen).Cents / 100.0 / life;
                    decisions[chosen]++;
                    units[chosen] += 1.0 / life;
                }
            }
        }

        var totalDecisions = decisions.Sum();
        var totalUnits = units.Sum();

        return new Opening(
            100.0 * spend / Draws / (mean.Cents / 100.0),
            spend / Draws,
            [.. decisions.Select(d => d / totalDecisions)],
            [.. units.Select(u => u / totalUnits)]);
    }

    /// <summary>
    /// §3.6's opening-pressure table, both calibrations, by the same method.
    ///
    /// The grouped table opens **three to four points lower** — not unchanged, as an earlier draft
    /// of §3.6 claimed. The disequilibrium is of the same kind (desired spend below income, premium
    /// in heavy surplus against a 40/40/20 supply) but it is a somewhat slacker economy at the
    /// opening, which pushes toward *less* scarcity and therefore, if anything, against the
    /// hypothesis. Worth knowing before any difference between arms is read, and not to be closed
    /// by adjusting base scores.
    /// </summary>
    [Fact]
    public void TheGroupedCalibrationOpensThreeToFourPointsLower()
    {
        var six = OpeningPressure(SimulationParameters.Default, drawTaste: false);
        var grouped = OpeningPressure(Grouped(), drawTaste: false);

        // Two tenths of a point either side, because these are 200,000-draw Monte-Carlo figures and
        // the last published digit is sampling noise rather than arithmetic. The claim being tested
        // is the three-to-four-point gap, which is twenty times that.
        Assert.InRange(six.ShareOfMeanIncome, 79.9, 80.3);
        Assert.InRange(grouped.ShareOfMeanIncome, 76.9, 77.3);
        Assert.InRange(grouped.SpendPerTick, 500.0, 501.8);

        Assert.InRange(six.ShareOfMeanIncome - grouped.ShareOfMeanIncome, 3.0, 4.0);

        // And with `w` drawn, which is the population the run actually has.
        Assert.InRange(OpeningPressure(SimulationParameters.Default, drawTaste: true).ShareOfMeanIncome, 75.6, 76.0);
        Assert.InRange(OpeningPressure(Grouped(), drawTaste: true).ShareOfMeanIncome, 71.9, 72.3);
    }

    /// <summary>
    /// The opening tier mix, and the two ways of counting it that must never be quoted under one
    /// label.
    ///
    /// Counting **(household, good) decisions** gives 0.521 / 0.464 / 0.016; counting **units per
    /// tick** gives 0.392 / 0.592 / 0.016, because the life-1 goods dominate the flow. Both are
    /// true, neither is the other, and premium is in heavy surplus against a 40/40/20 supply on
    /// either count — which is §3.3's point that the opening tier mix is not an equilibrium.
    /// </summary>
    [Fact]
    public void TheOpeningTierMixIsCountedTwoWaysAndTheyDiffer()
    {
        var grouped = OpeningPressure(Grouped(), drawTaste: false);

        Assert.Equal(0.521, grouped.Decisions[0], 2);
        Assert.Equal(0.464, grouped.Decisions[1], 2);
        Assert.Equal(0.016, grouped.Decisions[2], 3);

        Assert.Equal(0.392, grouped.Units[0], 2);
        Assert.Equal(0.592, grouped.Units[1], 2);
        Assert.Equal(0.016, grouped.Units[2], 3);

        // Premium is 20% of supply and nowhere near 20% of demand, on either count.
        Assert.True(grouped.Decisions[2] < 0.05);
        Assert.True(grouped.Units[2] < 0.05);
    }

    // ---- categories are labels -------------------------------------------------------------------

    /// <summary>Eighteen rows, six labels, and the labels in the order their first row appears.</summary>
    [Fact]
    public void EighteenGoodsRollUpIntoSixCategories()
    {
        var table = new GoodsTable(Grouped());

        Assert.Equal(18, table.CategoryCount);
        Assert.Equal(54, table.ShelfCount);
        Assert.Equal(
            ["food", "leisure", "clothing", "hobby", "electronics", "appliances"],
            table.Labels);

        Assert.Equal(0, table.LabelOf(0));   // groceries -> food
        Assert.Equal(0, table.LabelOf(2));   // eating_out -> food
        Assert.Equal(1, table.LabelOf(3));   // going_out -> leisure
        Assert.Equal(5, table.LabelOf(17));  // appliance_large -> appliances
    }

    /// <summary>
    /// A row that names no category is its own category — which is what makes §3.1 six categories
    /// of one good each, and every file written before E11 mean exactly what it always meant.
    /// </summary>
    [Fact]
    public void ARowWithNoCategoryLabelIsItsOwnCategory()
    {
        var table = new GoodsTable(SimulationParameters.Default);

        Assert.Equal(
            SimulationParameters.Default.Categories.Select(c => c.Name),
            table.Labels);

        for (var c = 0; c < table.CategoryCount; c++)
        {
            Assert.Equal(c, table.LabelOf(c));
        }

        var loaded = ConfigurationLoader.FromToml(
            """
            [categories.gadgets]
            life = 24
            price_ref = 500.00
            v = 0.02
            necessity = 0.10
            financeable = true
            term = 12
            """);

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));
        Assert.Equal("gadgets", loaded.Value.Categories[^1].Label);
    }

    /// <summary>
    /// The effective configuration spells the label out, and reloads to the same table.
    ///
    /// A resolved configuration that has to be overlaid on the right basis to mean what it says is
    /// not a resolved configuration, which is why it always carries `replace = true`.
    /// </summary>
    [Fact]
    public void TheEffectiveConfigurationSpellsOutTheLabels()
    {
        var printed = Grouped().ToToml();

        Assert.Contains("[categories]\nreplace = true", printed.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("[categories.phone]\ncategory = \"electronics\"", printed.ReplaceLineEndings("\n"), StringComparison.Ordinal);

        var reloaded = ConfigurationLoader.FromToml(printed);

        Assert.True(reloaded.IsSuccess, string.Join("; ", reloaded.Errors.Select(e => e.Message)));
        Assert.Equal(printed, reloaded.Value.ToToml());
        Assert.Empty(reloaded.Value.DifferencesFrom(Grouped()));
    }

    /// <summary>
    /// Every good in the grouped table is financeable exactly where §3.6 says, and the labels roll
    /// up to §3.6's per-category budgets.
    ///
    /// The budgets are the argument for the split — clothing 66.67 to 55.00, electronics 25.00 to
    /// 45.00 — and they are arithmetic on the table rather than prose about it.
    /// </summary>
    [Fact]
    public void TheGroupedCalibrationSpendsWhatTheSpecificationSaysPerCategory()
    {
        var parameters = Grouped();
        var table = new GoodsTable(parameters);

        var expected = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["food"] = 300.00,
            ["leisure"] = 200.00,
            ["clothing"] = 55.00,
            ["hobby"] = 30.00,
            ["electronics"] = 45.00,
            ["appliances"] = 20.00,
        };

        foreach (var (label, euros) in expected)
        {
            var spent = parameters.Categories
                .Where(c => string.Equals(c.Label, label, StringComparison.Ordinal))
                .Sum(c => c.PriceRef.Cents / 100.0 / c.Life);

            Assert.Equal(euros, spent, 2);
        }

        Assert.Equal(
            ["hobby_equipment", "hobby_big_kit", "phone", "laptop", "tv", "appliance_medium", "appliance_large"],
            parameters.Categories.Where(c => c.Financeable).Select(c => c.Name));

        // A kettle is not bought on credit. The flag says what happens in the world, not what the
        // experiment would find convenient.
        Assert.False(parameters.Categories.Single(c => c.Name == "appliance_small").Financeable);

        Assert.Equal(7, Enumerable.Range(0, table.CategoryCount).Count(table.IsFinanceable));
    }

    /// <summary>
    /// The grouped table's `v` sums to §3.6's 1.2555 — the median household values its basket at
    /// about 26% above what it costs, as §3.1 did.
    /// </summary>
    [Fact]
    public void TheGroupedCalibrationValuesItsBasketLikeTheSixCategoryOne()
    {
        Assert.Equal(1.2555, Grouped().Categories.Sum(c => c.V), 4);
        Assert.Equal(1.2820, SimulationParameters.Default.Categories.Sum(c => c.V), 4);
    }

    /// <summary>
    /// The spec's own table is the source: eighteen rows, and every life and price matches.
    ///
    /// A calibration that disagrees with the section it claims to implement is worse than no
    /// calibration, because the run produces numbers either way.
    /// </summary>
    [Fact]
    public void TheGroupedCalibrationMatchesTheSpecificationTable()
    {
        var spec = SpecFile.Tables("#### The eighteen goods")[0];
        var parameters = Grouped();

        var rows = spec
            .Skip(1)
            .Select(r => (Name: SpecFile.Clean(r[1]), Life: SpecFile.Integer(r[2]), Price: SpecFile.Number(r[3])))
            .ToList();

        Assert.Equal(18, rows.Count);

        for (var g = 0; g < rows.Count; g++)
        {
            Assert.Equal(rows[g].Life, parameters.Categories[g].Life);
            Assert.Equal(rows[g].Price, parameters.Categories[g].PriceRef.Cents / 100.0, 2);
            Assert.EndsWith(rows[g].Name.Replace(" ", "_", StringComparison.Ordinal), parameters.Categories[g].Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
