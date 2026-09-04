using System.Globalization;
using System.Text;
using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/02-03. The two identities here are what the calibration rests on.</summary>
public sealed class GoodsTableTests
{
    private static readonly SimulationParameters Parameters = SimulationParameters.Default;

    private static readonly GoodsTable Table = new(Parameters);

    // ---- the Stone-Geary split ------------------------------------------------------------

    /// <summary>
    /// `a_g + b_g · mean_income == v_g · mean_income`, for every category.
    ///
    /// The split being exactly neutral at the mean is what lets `necessity_g` be tuned without
    /// disturbing the calibration: it changes the income gradient of demand and nothing else. If
    /// this ever fails, someone has typed `a_g` in by hand.
    /// </summary>
    [Fact]
    public void TheValueSplit_IsNeutralAtTheMeanIncome()
    {
        for (var c = 0; c < Table.CategoryCount; c++)
        {
            var atTheMean = Table.Floor(c) + Table.MeanIncome.Scaled(Table.IncomeSlope(c));

            Assert.Equal(Table.MeanIncome.Scaled(Parameters.Categories[c].V), atTheMean);
        }
    }

    /// <summary>The a_g / b_g table in §3.1, which is derived output and can therefore be checked.</summary>
    [Fact]
    public void TheDerivedFloorsAndSlopes_MatchTheSpecification()
    {
        var spec = SpecFile.Tables("### 3.1 Categories")[1];

        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["food"] = "Food",
            ["leisure"] = "Leisure",
            ["clothing"] = "Clothing",
            ["hobby"] = "Hobby items",
            ["electronics"] = "Consumer electronics",
            ["appliances"] = "Appliances",
        };

        var rows = spec.Skip(1).ToDictionary(r => SpecFile.Clean(r[0]), r => r, StringComparer.OrdinalIgnoreCase);

        for (var c = 0; c < Table.CategoryCount; c++)
        {
            var row = rows[byName[Parameters.Categories[c].Name]];

            Assert.Equal(SpecFile.Number(row[1]), Table.Floor(c).Cents / 100.0);
            Assert.Equal(SpecFile.Number(row[2]), Table.IncomeSlope(c), 6);

            // The last column is flow_value at the mean income: a_g + b_g · 650.
            var atTheMean = Table.Floor(c) + Table.MeanIncome.Scaled(Table.IncomeSlope(c));
            Assert.Equal(SpecFile.Number(row[3]), atTheMean.Cents / 100.0);
        }
    }

    // ---- units ------------------------------------------------------------------------------

    [Fact]
    public void CapacityIsTheSteadyStateReplacementDemand()
    {
        foreach (var category in Parameters.Categories)
        {
            Assert.Equal(
                (int)Math.Round((double)Parameters.Run.Households / category.Life, MidpointRounding.AwayFromZero),
                category.Capacity);
        }
    }

    /// <summary>
    /// The units in a category's three tiers add up to its capacity, exactly.
    ///
    /// Total units staying at capacity is what makes unit demand and unit supply match by
    /// construction. A rounding residue here is a unit of supply that appears from nowhere or
    /// vanishes, every tick, for thirty years.
    /// </summary>
    [Fact]
    public void TierUnits_SumToCapacity()
    {
        for (var c = 0; c < Table.CategoryCount; c++)
        {
            var total = 0;
            for (var t = 0; t < Table.TierCount; t++)
            {
                total += Table.Units(c, t);
            }

            Assert.Equal(Parameters.Categories[c].Capacity, total);
        }
    }

    [Fact]
    public void TierUnits_AreTheSpecifiedSplit()
    {
        Assert.Equal([400, 400, 200], Row(c: 0));   // food, capacity 1000
        Assert.Equal([67, 67, 33], Row(c: 2));      // clothing, capacity 167
        Assert.Equal([33, 33, 17], Row(c: 3));      // hobby, capacity 83
        Assert.Equal([11, 11, 6], Row(c: 4));       // electronics, capacity 28
        Assert.Equal([4, 4, 2], Row(c: 5));         // appliances, capacity 10

        int[] Row(int c) => [.. Enumerable.Range(0, Table.TierCount).Select(t => Table.Units(c, t))];
    }

    /// <summary>
    /// Largest remainder and plain rounding agree on every value the specification uses, which is
    /// why `units(g,t) = round(unit_share_t · capacity_g)` reads as it does. Largest remainder is
    /// used because it also guarantees the sum, which rounding does not.
    /// </summary>
    [Fact]
    public void TheAllocationAgreesWithRoundingOnTheSpecifiedValues()
    {
        for (var c = 0; c < Table.CategoryCount; c++)
        {
            for (var t = 0; t < Table.TierCount; t++)
            {
                var rounded = (int)Math.Round(
                    Parameters.Tiers[t].UnitShare * Parameters.Categories[c].Capacity,
                    MidpointRounding.AwayFromZero);

                Assert.Equal(rounded, Table.Units(c, t));
            }
        }
    }

    // ---- the value identity ------------------------------------------------------------------

    /// <summary>
    /// Capacity value equals what the town earns, to within a tenth of a per cent.
    ///
    /// This is the reason the pool does not drain in equilibrium: whenever the market clears at
    /// any tier mix, nominal output equals nominal income. It holds because the unit shares times
    /// the price multipliers sum to exactly 1.000 — splitting capacity by value instead of by
    /// units breaks it, and the symptom appears fifty ticks later as a pool going to zero.
    /// </summary>
    [Fact]
    public void CapacityValue_MatchesTotalIncome()
    {
        var income = Parameters.Income.MeanIncome * Parameters.Run.Households;
        var capacity = Table.CapacityValue;

        Assert.Equal(Money.FromEuros(650_240), capacity);
        Assert.Equal(Money.FromEuros(650_000), income);

        var gap = Math.Abs(capacity.Cents - income.Cents) / (double)income.Cents;
        Assert.True(gap < 0.001, $"Capacity value is {gap:P3} away from income, which is more than 0.1%.");
    }

    [Fact]
    public void TheUnitSharesTimesThePriceMultipliers_SumToOne()
    {
        var total = Parameters.Tiers.Sum(t => t.UnitShare * t.PriceMult);

        Assert.Equal(1.0, total, 12);
    }

    /// <summary>The headline calibration: one standard basket per tick costs exactly the mean income.</summary>
    [Fact]
    public void TheStandardBasket_CostsTheMeanIncome()
    {
        Assert.Equal(650.00, Table.StandardBasketFlowCost, 2);
    }

    // ---- prices are derived ----------------------------------------------------------------

    [Fact]
    public void OpeningPrices_AreThePriceReferenceTimesTheTierMultiplier()
    {
        for (var c = 0; c < Table.CategoryCount; c++)
        {
            for (var t = 0; t < Table.TierCount; t++)
            {
                Assert.Equal(
                    Parameters.Categories[c].PriceRef.Scaled(Parameters.Tiers[t].PriceMult),
                    Table.OpeningPrice(c, t));
            }
        }

        // Food, spelled out: 180.00 / 300.00 / 540.00.
        Assert.Equal(Money.FromEuros(180), Table.OpeningPrice(0, 0));
        Assert.Equal(Money.FromEuros(300), Table.OpeningPrice(0, 1));
        Assert.Equal(Money.FromEuros(540), Table.OpeningPrice(0, 2));
    }

    // ---- categories are data ------------------------------------------------------------------

    /// <summary>
    /// Adding a category is a row in a configuration file. If it ever needs a code change, the
    /// engine has grown a branch on category identity and the goods table has stopped being data.
    /// </summary>
    [Fact]
    public void ASeventhCategory_NeedsNoCodeChange()
    {
        var result = ConfigurationLoader.FromToml(
            """
            [categories.gadgets]
            life = 24
            price_ref = 500.00
            v = 0.02
            necessity = 0.10
            financeable = true
            term = 12
            """);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));

        var table = new GoodsTable(result.Value);

        Assert.Equal(7, table.CategoryCount);
        Assert.Equal(21, table.ShelfCount);

        var gadgets = table.CategoryCount - 1;
        Assert.Equal(42, result.Value.Categories[gadgets].Capacity);       // round(1000 / 24)
        Assert.Equal(Money.FromEuros(300), table.OpeningPrice(gadgets, 0)); // 500 × 0.60
        Assert.Equal(Money.FromEuros(900), table.OpeningPrice(gadgets, 2)); // 500 × 1.80

        var units = Enumerable.Range(0, table.TierCount).Sum(t => table.Units(gadgets, t));
        Assert.Equal(42, units);

        Assert.True(table.IsDurable(gadgets));
        Assert.True(table.IsFinanceable(gadgets));
    }

    /// <summary>
    /// Eleven goods — neither six nor eighteen — run a tick.
    ///
    /// The count is the point. §3.1 has six rows and §3.6 has eighteen, and either could be read
    /// off a buffer sized once and reused, so a table of a length nobody designed for is the only
    /// thing that says nothing reads a fixed count. Eleven also makes `capacity` split unevenly
    /// across three tiers, which is where a table sized by `round(share × capacity)` rather than by
    /// the largest-remainder rule stops summing to capacity.
    /// </summary>
    [Fact]
    public void AGoodsTableOfAnyLength_RunsATick()
    {
        var lives = new[] { 1, 1, 2, 3, 4, 5, 7, 11, 19, 37, 61 };

        var toml = new StringBuilder();
        toml.AppendLine("[run]");
        toml.AppendLine("households = 400");
        toml.AppendLine("ticks = 12");
        toml.AppendLine("warmup_ticks = 4");
        toml.AppendLine();
        toml.AppendLine("[categories]");
        toml.AppendLine("replace = true");

        // Sum price_ref / life comes to the mean income, so the town is calibrated like any other.
        // 650 / 11 each, priced so that every row's flow cost is the same.
        foreach (var (life, i) in lives.Select((l, i) => (l, i)))
        {
            toml.AppendLine();
            toml.AppendLine(CultureInfo.InvariantCulture, $"[categories.good_{i}]");
            toml.AppendLine(CultureInfo.InvariantCulture, $"category = \"group_{i % 3}\"");
            toml.AppendLine(CultureInfo.InvariantCulture, $"life = {life}");
            toml.AppendLine(CultureInfo.InvariantCulture, $"price_ref = {650.0 / lives.Length * life:0.00}");
            toml.AppendLine(CultureInfo.InvariantCulture, $"v = {1.2 / lives.Length:0.000000}");
            toml.AppendLine("necessity = 0.4");
            toml.AppendLine("financeable = false");
            toml.AppendLine("term = 0");
        }

        var loaded = ConfigurationLoader.FromToml(toml.ToString());

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        var parameters = loaded.Value;
        var table = new GoodsTable(parameters);

        Assert.Equal(11, table.CategoryCount);
        Assert.Equal(33, table.ShelfCount);
        Assert.Equal(3, table.Labels.Count);

        for (var c = 0; c < table.CategoryCount; c++)
        {
            var units = Enumerable.Range(0, table.TierCount).Sum(t => table.Units(c, t));

            Assert.Equal(parameters.Categories[c].Capacity, units);
        }

        var simulation = new Simulation(parameters, 1);

        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= 3; tick++)
        {
            var ran = simulation.RunTick(tick);

            Assert.True(ran.IsSuccess, ran.IsFailed ? ran.Errors[0].Message : "");
        }
    }

    /// <summary>
    /// No enum of categories, and no switch on one. The failure this prevents is not a bug, it is
    /// a shape: once category identity is in the type system, every later mechanism gets a branch
    /// per category and the goods table stops being something a scenario file can change.
    /// </summary>
    [Fact]
    public void TheEngineHasNoEnumOfCategories()
    {
        var names = SimulationParameters.Default.Categories.Select(c => c.Name).ToArray();
        var offenders = new List<string>();

        foreach (var path in Repo.EngineSources())
        {
            var text = File.ReadAllText(path);

            // Sections.cs holds the default table itself; the names belong there as data.
            if (Path.GetFileName(path) == "Sections.cs")
            {
                continue;
            }

            offenders.AddRange(
                names
                    .Where(n => text.Contains($"\"{n}\"", StringComparison.OrdinalIgnoreCase))
                    .Select(n => $"{Path.GetFileName(path)}: \"{n}\""));

            if (text.Contains("enum Category", StringComparison.Ordinal))
            {
                offenders.Add($"{Path.GetFileName(path)}: enum Category");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "The engine names a category, so it is no longer data: " + string.Join("; ", offenders));
    }

    // ---- grid membership is computed ------------------------------------------------------------

    [Fact]
    public void DurableAndFinanceableAreComputed_NotLabelled()
    {
        Assert.False(Table.IsDurable(0));  // food, life 1
        Assert.False(Table.IsDurable(1));  // leisure, life 1
        Assert.True(Table.IsDurable(2));   // clothing, life 6

        // Clothing is the control the specification wants: a durable that cannot be financed.
        Assert.True(Table.IsDurable(2));
        Assert.False(Table.IsFinanceable(2));
    }
}
