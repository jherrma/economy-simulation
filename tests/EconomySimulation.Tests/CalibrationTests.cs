using EconomySimulation.Engine.Configuration;
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
