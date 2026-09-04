using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/07-01. Two CSV files, the configuration beside them, and a marker.</summary>
public sealed class MetricsWriterTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    /// <summary>A run into a temporary directory, cleaned up whatever happens.</summary>
    private static void InADirectory(Action<string> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), "economy-sim-" + Guid.NewGuid().ToString("N"));

        try
        {
            body(directory);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>Runs `ticks` ticks into `directory`, finishing unless `halt` says otherwise.</summary>
    private static void Run(string directory, int ticks, SimulationParameters? parameters = null, int seed = 1, bool finish = true)
    {
        var settings = parameters ?? Defaults;
        var simulation = new Simulation(settings, seed);
        Assert.True(simulation.Start().IsSuccess);

        var created = MetricsWriter.Create(simulation, directory);
        Assert.True(created.IsSuccess);

        using var writer = created.Value;

        for (var tick = 1; tick <= ticks; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");
            Assert.True(writer.Write(simulation).IsSuccess);
        }

        if (finish)
        {
            Assert.True(writer.Finish().IsSuccess);
        }
    }

    // ---- the cohort file (10-04) -----------------------------------------------------------------

    /// <summary>
    /// `cohorts.csv` sums to `run.csv`'s cohort block, every tick, every measure.
    ///
    /// This is what says the two-dimensional cut is the same measurement seen twice rather than two
    /// measurements. `run.csv` did not change to make room for the archetype dimension — V5a's whole
    /// claim is that it is byte-identical under the identity table — so the long file has to be
    /// reconcilable with it or the dataset carries two answers.
    /// </summary>
    [Fact]
    public void TheCohortFileSumsToTheRunFile()
    {
        InADirectory(directory =>
        {
            var typed = Typed();

            Run(directory, ticks: 6, typed);

            var run = Csv.Read(Path.Combine(directory, "run.csv"));
            var cells = Csv.Read(Path.Combine(directory, "cohorts.csv"));
            var archetypes = typed.Archetypes.Types.Count;

            Assert.Equal(4, archetypes);
            Assert.Equal(run.RowCount * 2 * archetypes, cells.RowCount);

            string[] measures =
            [
                "households", "cash", "loans_outstanding", "debt_service", "spend", "quality",
                "wanted", "obtained",
                "food_wanted", "food_obtained", "food_spend", "food_budget_units",
                "electronics_obtained", "electronics_premium_units",
            ];

            foreach (var tick in run.Rows())
            {
                foreach (var cohort in new[] { "abstainer", "borrower" })
                {
                    var rows = cells
                        .Where("tick", run.Text(tick, "tick"))
                        .Where(r => string.Equals(cells.Text(r, "cohort"), cohort, StringComparison.Ordinal))
                        .ToArray();

                    Assert.Equal(archetypes, rows.Length);

                    foreach (var measure in measures)
                    {
                        Assert.Equal(
                            run.Number(tick, $"{cohort}_{measure}"),
                            rows.Sum(r => cells.Number(r, measure)),
                            6);
                    }
                }
            }
        });
    }

    /// <summary>
    /// The median does not sum, and is not asked to. `run.csv`'s cohort median is the median over
    /// the cohort's whole population — taken from the summed histogram, not from the cells' answers
    /// — so it lies between the smallest and the largest of them and generally equals none.
    /// </summary>
    [Fact]
    public void TheCohortMedianIsNotTheSumOfTheCellMedians()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 6, Typed());

            var run = Csv.Read(Path.Combine(directory, "run.csv"));
            var cells = Csv.Read(Path.Combine(directory, "cohorts.csv"));

            foreach (var tick in run.Rows())
            {
                var rows = cells
                    .Where("tick", run.Text(tick, "tick"))
                    .Where(r => string.Equals(cells.Text(r, "cohort"), "abstainer", StringComparison.Ordinal))
                    .ToArray();

                var overall = run.Number(tick, "abstainer_wait_median");

                Assert.InRange(
                    overall,
                    rows.Min(r => cells.Number(r, "wait_median")),
                    rows.Max(r => cells.Number(r, "wait_median")));
            }
        });
    }

    /// <summary>
    /// **No share is written anywhere but the tier mix.** Counts only, so a share computed from this
    /// file is necessarily computed *within* a category. Pooling a share across categories reverses
    /// its sign when exclusion moves units out of the denominator (`01-SIMULATION.md` §10.4), and
    /// the archetypes were designed to differ in exactly the category weights a pooled figure
    /// averages over — so this file is where that trap would be laid.
    /// </summary>
    [Fact]
    public void TheCohortFileCarriesNoShares()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 2, Typed());

            var cells = Csv.Read(Path.Combine(directory, "cohorts.csv"));

            Assert.DoesNotContain(cells.Columns, c => c.Contains("share", StringComparison.Ordinal));
            Assert.Contains("archetype", cells.Columns);

            // And the archetype is on every row, so a reader needs no knowledge of how many
            // types there were.
            Assert.All(cells.Rows(), r => Assert.NotEmpty(cells.Text(r, "archetype")));
        });
    }

    /// <summary>Under the identity table there is one type, called `average`, and the file is `run.csv` again.</summary>
    [Fact]
    public void UnderTheIdentityTableThereIsOneCellPerCohort()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 3);

            var run = Csv.Read(Path.Combine(directory, "run.csv"));
            var cells = Csv.Read(Path.Combine(directory, "cohorts.csv"));

            Assert.Equal(run.RowCount * 2, cells.RowCount);
            Assert.All(cells.Rows(), r => Assert.Equal("average", cells.Text(r, "archetype")));
        });
    }

    private static SimulationParameters Typed()
    {
        var scenario = Scenario.FromFile(
            Path.Combine(Repo.Root, "config", "scenarios", "typed_credit_high.toml"));

        Assert.True(scenario.IsSuccess, string.Join("; ", scenario.Errors.Select(e => e.Message)));

        return scenario.Value.Parameters;
    }

    /// <summary>The grouped calibration of §3.6: eighteen goods rolling up into six categories.</summary>
    private static SimulationParameters Grouped()
    {
        var loaded = ConfigurationLoader.FromFile(
            Path.Combine(Repo.Root, "config", "calibrations", "grouped.toml"));

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        return loaded.Value with { Run = loaded.Value.Run with { Ticks = 12, WarmupTicks = 4 } };
    }

    // ---- the category roll-up (11-01) -------------------------------------------------------------

    /// <summary>
    /// `cpi_category_x` is the unit-weighted roll-up of the `cpi_g` of the goods labelled `x`.
    ///
    /// Unit-weighted, and the test says so in the arithmetic rather than in a comment: the weights
    /// are each good's opening value at supply, so a €1,440 washing machine replaced every twelve
    /// years counts for its seven units. Value-weighting instead would let one expensive,
    /// rarely-replaced good speak for a category of three, and the two diverge sharply on exactly
    /// the categories §3.6 splits.
    /// </summary>
    [Fact]
    public void TheCategoryIndexIsTheUnitWeightedRollUpOfItsGoods()
    {
        InADirectory(directory =>
        {
            var parameters = Grouped();

            Run(directory, ticks: 8, parameters);

            var goods = new GoodsTable(parameters);
            var run = Csv.Read(Path.Combine(directory, "run.csv"));
            var tiers = Csv.Read(Path.Combine(directory, "tiers.csv"));

            Assert.Equal(18, goods.CategoryCount);
            Assert.Equal(6, goods.Labels.Count);

            // Rebuilt from the shelf prices rather than from the per-good index, so that the two
            // sides of this assertion do not share an intermediate. `cpi_g` is written to six
            // decimals, and a roll-up of six-decimal numbers is not the roll-up.
            var traded = new Dictionary<(string Tick, string Good, string Tier), double>();

            foreach (var row in tiers.Rows())
            {
                traded[(tiers.Text(row, "tick"), tiers.Text(row, "good"), tiers.Text(row, "tier"))] =
                    tiers.Number(row, "price");
            }

            foreach (var tick in run.Rows())
            {
                var when = run.Text(tick, "tick");

                for (var l = 0; l < goods.Labels.Count; l++)
                {
                    var now = 0.0;
                    var opening = 0.0;

                    for (var c = 0; c < goods.CategoryCount; c++)
                    {
                        if (goods.LabelOf(c) != l)
                        {
                            continue;
                        }

                        for (var t = 0; t < goods.TierCount; t++)
                        {
                            var units = goods.Units(c, t);

                            now += traded[(when, parameters.Categories[c].Name, parameters.Tiers[t].Name)] * units;
                            opening += goods.OpeningPrice(c, t).Cents / 100.0 * units;
                        }
                    }

                    Assert.Equal(now / opening, run.Number(tick, "cpi_category_" + goods.Labels[l]), 6);
                }
            }
        });
    }

    /// <summary>
    /// Under §3.1 every row is its own category, so the roll-up is the good's own index — the one
    /// table where it cannot be wrong, which is what makes it worth asserting.
    /// </summary>
    [Fact]
    public void UnderTheDefaultCalibrationTheRollUpIsTheGoodItself()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 4);

            var run = Csv.Read(Path.Combine(directory, "run.csv"));
            var tiers = Csv.Read(Path.Combine(directory, "tiers.csv"));

            foreach (var good in Defaults.Categories.Select(c => c.Name))
            {
                foreach (var tick in run.Rows())
                {
                    Assert.Equal(run.Text(tick, "cpi_" + good), run.Text(tick, "cpi_category_" + good));
                }
            }

            // And the same of the tier mix, and of the two columns naming the row.
            foreach (var row in tiers.Rows())
            {
                Assert.Equal(tiers.Text(row, "category"), tiers.Text(row, "good"));
                Assert.Equal(tiers.Text(row, "mix_share"), tiers.Text(row, "category_mix_share"));
            }
        });
    }

    /// <summary>
    /// The tier mix is reported **within** a good and **within** a category, and the two are
    /// different numbers once a category holds more than one good.
    ///
    /// Both denominators are checked, because the failure this guards is not an arithmetic slip —
    /// it is a share whose denominator is wider than it looks. §10.4 found a pooled tier share
    /// reporting the effect backwards when exclusion moved units out of the denominator, and one
    /// category is the widest denominator this model will report a share over.
    /// </summary>
    [Fact]
    public void TheTierMixIsReportedWithinAGoodAndWithinACategory()
    {
        InADirectory(directory =>
        {
            var parameters = Grouped();

            Run(directory, ticks: 8, parameters);

            var goods = new GoodsTable(parameters);
            var tiers = Csv.Read(Path.Combine(directory, "tiers.csv"));

            Assert.Equal(18 * 3 * 8, tiers.RowCount);

            var names = parameters.Categories.Select(c => c.Name).ToList();

            var perGood = new Dictionary<(string Tick, string Good), double>();

            // Keyed by tier as well, because `category_mix_share` is a property of the category and
            // is written on all three of its goods' rows. Summing the column would count it thrice.
            var perCategory = new Dictionary<(string Tick, string Category, string Tier), double>();
            var differs = 0;

            foreach (var row in tiers.Rows())
            {
                var tick = tiers.Text(row, "tick");
                var good = tiers.Text(row, "good");
                var category = tiers.Text(row, "category");
                var tier = tiers.Text(row, "tier");
                var within = tiers.Number(row, "mix_share");
                var across = tiers.Number(row, "category_mix_share");

                perGood.TryGetValue((tick, good), out var g);
                perGood[(tick, good)] = g + within;

                if (perCategory.TryGetValue((tick, category, tier), out var seen))
                {
                    Assert.Equal(seen, across);
                }

                perCategory[(tick, category, tier)] = across;

                Assert.Equal(goods.Labels[goods.LabelOf(names.IndexOf(good))], category);

                if (Math.Abs(within - across) > 1e-9)
                {
                    differs++;
                }
            }

            var categoryTotals = perCategory
                .GroupBy(e => (e.Key.Tick, e.Key.Category))
                .Select(g => g.Sum(e => e.Value));

            // Each denominator sums to 1 over its own tiers, or to 0 where nothing sold at all.
            foreach (var total in perGood.Values.Concat(categoryTotals))
            {
                Assert.True(Math.Abs(total - 1.0) < 1e-5 || Math.Abs(total) < 1e-9, $"a mix share summed to {total}");
            }

            // Three goods per category, so the two shares are genuinely different numbers.
            Assert.True(differs > 0, "the within-good and within-category tier shares never differed");
        });
    }

    // ---- what is written ----------------------------------------------------------------------

    /// <summary>Every series the story names is present, and the shelf rows are one per tier per category per tick.</summary>
    [Fact]
    public void BothFilesCarryEverySpecifiedSeries()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 4);

            var tiers = Csv.Read(Path.Combine(directory, "tiers.csv"));
            var run = Csv.Read(Path.Combine(directory, "run.csv"));

            foreach (var column in new[] { "scenario", "seed", "tick", "warmup", "category", "good", "tier", "price", "units", "sold", "blocked", "unaffordable", "mix_share", "category_mix_share" })
            {
                Assert.True(tiers.Has(column), $"tiers.csv has no column '{column}'");
            }

            foreach (var column in new[] { "scenario", "seed", "tick", "warmup", "cpi", "cpi_food", "cpi_appliances", "money_stock", "loans_outstanding", "loans_live", "pool", "money_created", "money_destroyed", "rationed" })
            {
                Assert.True(run.Has(column), $"run.csv has no column '{column}'");
            }

            Assert.Equal(4, run.RowCount);
            Assert.Equal(4 * 6 * 3, tiers.RowCount);
        });
    }

    /// <summary>Every row carries the seed and the scenario, so files from different runs concatenate without ambiguity.</summary>
    [Fact]
    public void EveryRowCarriesTheSeedAndTheScenario()
    {
        InADirectory(directory =>
        {
            var high = Defaults with { Run = Defaults.Run with { Scenario = "credit_high" } };
            Run(directory, ticks: 2, high, seed: 17);

            foreach (var name in new[] { "tiers.csv", "run.csv" })
            {
                var csv = Csv.Read(Path.Combine(directory, name));

                Assert.NotEqual(0, csv.RowCount);
                Assert.All(csv.Rows(), r => Assert.Equal("credit_high", csv.Text(r, "scenario")));
                Assert.All(csv.Rows(), r => Assert.Equal(17, csv.Integer(r, "seed")));
            }
        });
    }

    /// <summary>
    /// Warm-up ticks are written and flagged, never silently discarded: the transient can only be
    /// checked against the data it happened in (V4).
    /// </summary>
    [Fact]
    public void WarmupTicksAreWrittenAndFlagged()
    {
        InADirectory(directory =>
        {
            var short_ = Defaults with { Run = Defaults.Run with { WarmupTicks = 3 } };
            Run(directory, ticks: 5, short_);

            var run = Csv.Read(Path.Combine(directory, "run.csv"));

            Assert.Equal(5, run.RowCount);
            Assert.Equal([1, 1, 1, 0, 0], run.Rows().Select(r => run.Integer(r, "warmup")).ToArray());
        });
    }

    /// <summary>The prices written are the ones the tick traded at, not the ones step 6 set for the next tick.</summary>
    [Fact]
    public void ThePriceWrittenIsThePriceTheTickTradedAt()
    {
        InADirectory(directory =>
        {
            var simulation = new Simulation(Defaults, runSeed: 1);
            Assert.True(simulation.Start().IsSuccess);

            var created = MetricsWriter.Create(simulation, directory);
            Assert.True(created.IsSuccess);

            using (var writer = created.Value)
            {
                for (var tick = 1; tick <= 3; tick++)
                {
                    var opening = simulation.Market.Price(0, 0);
                    Assert.True(simulation.RunTick(tick).IsSuccess);

                    // Step 6 has already moved food's budget price by the time the tick returns.
                    Assert.NotEqual(opening, simulation.Market.Price(0, 0));
                    Assert.Equal(opening, simulation.Recorded.PriceTraded(0, 0));
                    Assert.True(writer.Write(simulation).IsSuccess);
                }

                Assert.True(writer.Finish().IsSuccess);
            }

            var tiers = Csv.Read(Path.Combine(directory, "tiers.csv"));
            var first = tiers.Rows().First(r => tiers.Text(r, "category") == "food" && tiers.Text(r, "tier") == "budget");

            Assert.Equal("180.00", tiers.Text(first, "price"));
        });
    }

    /// <summary>The effective configuration is written beside the output, and it is the resolved one.</summary>
    [Fact]
    public void TheEffectiveConfigurationIsWrittenBesideTheOutput()
    {
        InADirectory(directory =>
        {
            var five = Defaults with { Run = Defaults.Run with { Households = 500, Scenario = "credit_low" } };
            Run(directory, ticks: 1, five);

            var written = File.ReadAllText(Path.Combine(directory, "effective-config.toml"));

            Assert.Contains("households = 500", written, StringComparison.Ordinal);
            Assert.Contains("scenario = \"credit_low\"", written, StringComparison.Ordinal);
            Assert.Contains("buffer_months = 2.0", written, StringComparison.Ordinal);
        });
    }

    // ---- the marker ---------------------------------------------------------------------------

    /// <summary>A clean finish leaves the marker, and it says what finished.</summary>
    [Fact]
    public void ACleanFinishWritesTheMarker()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 3);

            var marker = File.ReadAllText(Path.Combine(directory, MetricsWriter.MarkerFile));

            Assert.Contains("ticks = 3", marker, StringComparison.Ordinal);
            Assert.Contains("tier_rows = 54", marker, StringComparison.Ordinal);
            Assert.Contains("seed = 1", marker, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// A halted run leaves no marker. This is the criterion that protects the campaign: E9 collects
    /// hundreds of directories, and a partial CSV without a marker is refused rather than averaged
    /// into a result.
    /// </summary>
    [Fact]
    public void AHaltedRunLeavesNoMarker()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 3, finish: false);

            Assert.True(File.Exists(Path.Combine(directory, "run.csv")));
            Assert.False(File.Exists(Path.Combine(directory, MetricsWriter.MarkerFile)));

            // The rows are there — the writer flushed on dispose — which is exactly why the
            // presence of a file must not be what a collector trusts.
            Assert.Equal(3, Csv.Read(Path.Combine(directory, "run.csv")).RowCount);
        });
    }

    /// <summary>A marker left by an earlier run into the same directory is removed when the new one opens.</summary>
    [Fact]
    public void AStaleMarkerIsRemovedWhenTheDirectoryIsReopened()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 2);
            Assert.True(File.Exists(Path.Combine(directory, MetricsWriter.MarkerFile)));

            Run(directory, ticks: 2, finish: false);
            Assert.False(File.Exists(Path.Combine(directory, MetricsWriter.MarkerFile)));
        });
    }

    // ---- the schema ---------------------------------------------------------------------------

    /// <summary>
    /// The additive rule, held to a committed file: a `run.csv` written in September 2026 is still
    /// read by the current reader, every column it had still present and still meaning the same
    /// thing. Columns may be added; renaming or repurposing one breaks this.
    /// </summary>
    [Fact]
    public void AnOlderFileIsStillReadByTheCurrentReader()
    {
        var older = Csv.Read(Path.Combine(Repo.Root, "tests", "EconomySimulation.Tests", "fixtures", "run-2026-09.csv"));

        Assert.Equal(2, older.RowCount);
        Assert.Equal("credit_off", older.Text(0, "scenario"));
        Assert.Equal(1, older.Integer(0, "tick"));
        Assert.Equal(1, older.Integer(0, "warmup"));
        Assert.Equal("0.00", older.Text(1, "loans_outstanding"));
        Assert.Equal(7612345.00, older.Number(1, "pool"));

        InADirectory(directory =>
        {
            Run(directory, ticks: 1);
            var current = Csv.Read(Path.Combine(directory, "run.csv"));

            foreach (var column in older.Columns)
            {
                Assert.True(current.Has(column), $"column '{column}' was in the September 2026 schema and is gone");
            }
        });
    }

    /// <summary>The header and the row are written by two methods, and a row that disagrees with the header refuses to be written.</summary>
    [Fact]
    public void EveryRowHasExactlyAsManyFieldsAsTheHeader()
    {
        InADirectory(directory =>
        {
            Run(directory, ticks: 2);

            foreach (var name in new[] { "tiers.csv", "run.csv" })
            {
                var lines = File.ReadAllLines(Path.Combine(directory, name));
                var expected = lines[0].Split(',').Length;

                Assert.All(lines, l => Assert.Equal(expected, l.Split(',').Length));
            }
        });
    }

    // ---- writing does not sit in the way of the tick ------------------------------------------

    /// <summary>
    /// Writing is buffered: a hundred ticks of rows are still in the buffer, not in the file, until
    /// the writer is finished. A flush per row would be a syscall per row inside the loop the run
    /// is measured by.
    /// </summary>
    [Fact]
    public void WritingIsBufferedRatherThanFlushedPerRow()
    {
        InADirectory(directory =>
        {
            var simulation = new Simulation(Defaults, runSeed: 1);
            Assert.True(simulation.Start().IsSuccess);

            var created = MetricsWriter.Create(simulation, directory);
            Assert.True(created.IsSuccess);

            using var writer = created.Value;
            var path = Path.Combine(directory, "run.csv");

            for (var tick = 1; tick <= 100; tick++)
            {
                Assert.True(simulation.RunTick(tick).IsSuccess);
                Assert.True(writer.Write(simulation).IsSuccess);
            }

            var buffered = new FileInfo(path).Length;
            Assert.True(writer.Finish().IsSuccess);
            var flushed = new FileInfo(path).Length;

            Assert.True(buffered < flushed, $"{buffered} bytes were already on disk of the {flushed} the run wrote");
            Assert.Equal(100, Csv.Read(path).RowCount);
        });
    }

    /// <summary>Writing after finishing is refused rather than silently appending past the marker.</summary>
    [Fact]
    public void WritingAfterTheMarkerIsRefused()
    {
        InADirectory(directory =>
        {
            var simulation = new Simulation(Defaults, runSeed: 1);
            Assert.True(simulation.Start().IsSuccess);
            Assert.True(simulation.RunTick(1).IsSuccess);

            var created = MetricsWriter.Create(simulation, directory);
            Assert.True(created.IsSuccess);

            using var writer = created.Value;
            Assert.True(writer.Write(simulation).IsSuccess);
            Assert.True(writer.Finish().IsSuccess);

            var refused = writer.Write(simulation);

            Assert.True(refused.IsFailed);
            Assert.Contains("finished", refused.Errors[0].Message, StringComparison.Ordinal);
        });
    }
}
