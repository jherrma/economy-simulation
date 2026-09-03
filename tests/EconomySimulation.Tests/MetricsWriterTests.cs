using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
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

        var created = MetricsWriter.Create(settings, seed, directory);
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

            foreach (var column in new[] { "scenario", "seed", "tick", "warmup", "category", "tier", "price", "units", "sold", "blocked", "unaffordable" })
            {
                Assert.True(tiers.Has(column), $"tiers.csv has no column '{column}'");
            }

            foreach (var column in new[] { "scenario", "seed", "tick", "warmup", "money_stock", "loans_outstanding", "loans_live", "pool", "money_created", "money_destroyed", "rationed" })
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

            var created = MetricsWriter.Create(Defaults, 1, directory);
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

            var created = MetricsWriter.Create(Defaults, 1, directory);
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

            var created = MetricsWriter.Create(Defaults, 1, directory);
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
