using EconomySimulation.Campaign;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/09-02.</summary>
public sealed class CampaignTests
{
    private static IReadOnlyList<Scenario> Scenarios()
    {
        var all = Scenario.All(Path.Combine(Repo.Root, "config", "scenarios"));

        Assert.True(all.IsSuccess, string.Join("; ", all.Errors.Select(e => e.Message)));

        return all.Value;
    }

    private static void InADirectory(Action<string> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), "economy-simulation-campaign-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
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

    /// <summary>
    /// A run's output, written by hand. The collector's job is to refuse the malformed ones, so the
    /// tests have to be able to produce them — which a real run, by construction, cannot.
    /// </summary>
    private static void Write(
        string output,
        Fleet.Unit unit,
        string header = "scenario,seed,tick,warmup,cpi",
        string configuration = "k = 0.05",
        bool marker = true,
        int warmupTicks = 2,
        int measuredTicks = 3)
    {
        var directory = Layout.Run(output, unit.Scenario, unit.Seed);
        Directory.CreateDirectory(directory);

        foreach (var file in Collector.Files)
        {
            var lines = new List<string> { header };

            for (var tick = 1; tick <= warmupTicks + measuredTicks; tick++)
            {
                lines.Add($"{unit.Scenario},{unit.Seed},{tick},{(tick <= warmupTicks ? 1 : 0)},1.0");
            }

            File.WriteAllLines(Path.Combine(directory, file), lines);
        }

        File.WriteAllText(Path.Combine(directory, "effective-config.toml"), configuration);

        if (marker)
        {
            File.WriteAllText(Path.Combine(directory, MetricsWriter.MarkerFile), string.Empty);
        }
    }

    private static readonly Fleet.Unit[] Two =
        [new("credit_off", 1), new("credit_high", 1)];

    // ---- the pairing ---------------------------------------------------------------------------

    /// <summary>
    /// The criterion the whole campaign rests on: the same households abstain in every scenario, at
    /// every seed. If it ever fails, the headline difference is partly a different set of people and
    /// no amount of care downstream recovers it.
    /// </summary>
    [Fact]
    public void TheAbstainerSet_IsIdenticalAcrossScenarios_AtEverySeed()
    {
        var scenarios = Scenarios();
        var seeds = Enumerable.Range(1, scenarios[0].Parameters.Run.Seeds).ToArray();

        var paired = Pairing.Check(scenarios, seeds);

        Assert.True(paired.IsSuccess, string.Join("; ", paired.Errors.Select(e => e.Message)));
    }

    /// <summary>
    /// And the check can fail. A pairing test that cannot distinguish a moved abstainer set from an
    /// unmoved one is a line of output, not a check.
    /// </summary>
    [Fact]
    public void ThePairingCheck_CatchesAMovedAbstainerSet()
    {
        var scenarios = Scenarios();
        var moved = Scenario.FromToml("moved", "[run]\nabstainer_share = 0.4\n");

        Assert.True(moved.IsSuccess);

        var paired = Pairing.Check([scenarios[0], moved.Value], [1, 2]);

        Assert.True(paired.IsFailed);
        Assert.Contains("not paired", paired.Errors[0].Message, StringComparison.Ordinal);
    }

    // ---- the marker ----------------------------------------------------------------------------

    [Fact]
    public void ARunWithoutItsMarker_IsRefusedAndNamed()
    {
        InADirectory(output =>
        {
            Write(output, Two[0]);
            Write(output, Two[1], marker: false);

            var collected = Collector.Collect(Two, output);

            Assert.True(collected.IsFailed);
            Assert.Single(collected.Errors);
            Assert.Contains("credit_high seed 1", collected.Errors[0].Message, StringComparison.Ordinal);
            Assert.Contains(MetricsWriter.MarkerFile, collected.Errors[0].Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void EveryMissingMarker_IsNamed_NotJustTheFirst()
    {
        InADirectory(output =>
        {
            Write(output, Two[0], marker: false);
            Write(output, Two[1], marker: false);

            Assert.Equal(2, Collector.Collect(Two, output).Errors.Count);
        });
    }

    // ---- the dataset ---------------------------------------------------------------------------

    [Fact]
    public void OnlyTheMeasuredWindow_EntersTheDataset()
    {
        InADirectory(output =>
        {
            Write(output, Two[0], warmupTicks: 2, measuredTicks: 3);
            Write(output, Two[1], warmupTicks: 2, measuredTicks: 3);

            var collected = Collector.Collect(Two, output);

            Assert.True(collected.IsSuccess);
            Assert.Equal(6, collected.Value.Rows["run.csv"]);

            var dataset = File.ReadAllLines(Path.Combine(collected.Value.Directory, "run.csv"));

            Assert.Equal(7, dataset.Length);
            Assert.DoesNotContain(dataset.Skip(1), line => line.Split(',')[3] == "1");
        });
    }

    /// <summary>The warm-up is still in the per-run files: V4's claim can only be checked against it.</summary>
    [Fact]
    public void TheWarmUpRows_StayInThePerRunFiles()
    {
        InADirectory(output =>
        {
            Write(output, Two[0]);
            Write(output, Two[1]);

            Assert.True(Collector.Collect(Two, output).IsSuccess);

            var raw = File.ReadAllLines(Path.Combine(Layout.Run(output, "credit_off", 1), "run.csv"));

            Assert.Equal(2, raw.Skip(1).Count(line => line.Split(',')[3] == "1"));
        });
    }

    /// <summary>
    /// The pairing is recoverable from the data alone, and the collector adds nothing to it. A
    /// dataset with a column the runs do not have is a dataset carrying an analysis.
    /// </summary>
    [Fact]
    public void TheDataset_HasTheSameColumnsAsTheRuns_AndCarriesScenarioAndSeed()
    {
        InADirectory(output =>
        {
            Write(output, Two[0]);
            Write(output, Two[1]);

            var collected = Collector.Collect(Two, output);
            var dataset = File.ReadAllLines(Path.Combine(collected.Value.Directory, "run.csv"));
            var run = File.ReadAllLines(Path.Combine(Layout.Run(output, "credit_off", 1), "run.csv"));

            Assert.Equal(run[0], dataset[0]);
            Assert.StartsWith("scenario,seed,", dataset[0], StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RunsWithDifferentColumns_AreRefusedRatherThanConcatenated()
    {
        InADirectory(output =>
        {
            Write(output, Two[0]);
            Write(output, Two[1], header: "scenario,seed,tick,warmup,cpi,cpi_food");

            var collected = Collector.Collect(Two, output);

            Assert.True(collected.IsFailed);
            Assert.Contains("different header", collected.Errors[0].Message, StringComparison.Ordinal);
        });
    }

    /// <summary>The seed is not a parameter. If two seeds of one scenario disagree, it has become one.</summary>
    [Fact]
    public void SeedsOfOneScenarioThatDisagreeAboutTheConfiguration_AreRefused()
    {
        InADirectory(output =>
        {
            Fleet.Unit[] units = [new("credit_off", 1), new("credit_off", 2)];

            Write(output, units[0], configuration: "k = 0.05");
            Write(output, units[1], configuration: "k = 0.20");

            var collected = Collector.Collect(units, output);

            Assert.True(collected.IsFailed);
            Assert.Contains("different effective configuration", collected.Errors[0].Message, StringComparison.Ordinal);
        });
    }

    // ---- the manifest --------------------------------------------------------------------------

    [Fact]
    public void TheManifest_RecordsWhatWouldBeNeededToReproduceTheDataset()
    {
        InADirectory(output =>
        {
            Write(output, Two[0]);
            Write(output, Two[1]);

            var collected = Collector.Collect(Two, output);
            var started = DateTimeOffset.UtcNow;

            var path = Manifest.Write(
                output,
                ["credit_off", "credit_high"],
                [1],
                collected.Value,
                started,
                started.AddSeconds(30));

            var manifest = File.ReadAllText(path);

            Assert.Contains("engine_commit", manifest, StringComparison.Ordinal);
            Assert.Contains("working_tree_clean", manifest, StringComparison.Ordinal);
            Assert.Contains("seeds = [1]", manifest, StringComparison.Ordinal);
            Assert.Contains("runs = 2", manifest, StringComparison.Ordinal);
            Assert.Contains("[scenarios.credit_high]", manifest, StringComparison.Ordinal);
            Assert.Contains("effective_config_sha256", manifest, StringComparison.Ordinal);
            Assert.Contains(started.ToString("O", System.Globalization.CultureInfo.InvariantCulture), manifest, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The hash is of the configuration, so two scenarios that ran the same parameters hash the
    /// same and two that did not, do not. A manifest whose hashes never differ records nothing.
    /// </summary>
    [Fact]
    public void TheConfigurationHash_DistinguishesScenariosThatActuallyDiffer()
    {
        InADirectory(output =>
        {
            Write(output, Two[0], configuration: "k = 0.05");
            Write(output, Two[1], configuration: "k = 0.20");

            var collected = Collector.Collect(Two, output);
            var started = DateTimeOffset.UtcNow;

            var manifest = File.ReadAllText(Manifest.Write(
                output, ["credit_off", "credit_high"], [1], collected.Value, started, started));

            var hashes = manifest
                .Split('\n')
                .Where(line => line.StartsWith("effective_config_sha256", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.Equal(2, hashes.Count);
        });
    }

    // ---- the units -----------------------------------------------------------------------------

    [Fact]
    public void EveryScenarioMeetsEverySeed()
    {
        var units = Fleet.Units(["credit_off", "credit_high"], [1, 2, 3]);

        Assert.Equal(6, units.Count);
        Assert.Equal(6, units.Distinct().Count());
    }
}
