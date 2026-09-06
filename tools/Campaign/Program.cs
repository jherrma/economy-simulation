using EconomySimulation.Engine.Configuration;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Campaign;

/// <summary>
/// Every scenario over every seed, one process each, collected into one dataset.
///
/// Nothing is differenced, averaged or plotted here. The campaign produces a **dataset**, not a
/// result: the moment the runner starts computing the answer, the answer stops being something
/// anyone can check against the runs it came from.
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!Array.Exists(args, a => string.Equals(a, "--all", StringComparison.Ordinal)))
        {
            return Usage();
        }

        var output = Argument(args, "--into") ?? Layout.DefaultOutput;
        var calibration = Argument(args, "--calibration");
        var scenarios = Scenario.All(Layout.Scenarios);

        if (scenarios.IsFailed)
        {
            return Fail("the scenarios", scenarios.Errors);
        }

        // The calibration is the town, not an arm: it is the basis every scenario is laid on, and
        // every run of one campaign is in the same one or the comparison is between two towns.
        var basis = calibration is null
            ? Result.Ok(SimulationParameters.Default)
            : ConfigurationLoader.FromFile(calibration);

        if (basis.IsFailed)
        {
            return Fail("the calibration", basis.Errors);
        }

        var applied = scenarios.Value[0].ApplyTo(basis.Value);

        if (applied.IsFailed)
        {
            return Fail("the calibration", applied.Errors);
        }

        var parameters = applied.Value;
        var seeds = Enumerable.Range(1, parameters.Run.Seeds).ToArray();
        var names = scenarios.Value.Select(s => s.Name).ToArray();
        var units = Fleet.Units(names, seeds);

        var source = calibration ?? "the schema defaults";

        Console.Out.WriteLine(Invariant(
            $"campaign: {names.Length} scenarios x {seeds.Length} seeds = {units.Count} runs of {parameters.Run.Ticks} ticks, into {output}"));
        Console.Out.WriteLine(Invariant(
            $"calibration: {parameters.Categories.Count} goods, {parameters.Run.Households} households, from {source}"));

        // Before the runs, not after: a campaign whose arms are not paired is void, and finding
        // that out having spent the runs is finding it out too late to be useful.
        var paired = Pairing.Check(scenarios.Value, seeds, basis.Value);

        if (paired.IsFailed)
        {
            return Fail("the pairing", paired.Errors);
        }

        Console.Out.WriteLine(Invariant(
            $"pairing: the same abstainers in all {names.Length} scenarios, on all {seeds.Length} seeds"));

        var started = DateTimeOffset.UtcNow;
        var finished = 0;

        var ran = Fleet.RunAll(units, output, _ => Report(++finished, units.Count), calibration);

        Console.Out.WriteLine();

        if (ran.IsFailed)
        {
            return Fail("the runs", ran.Errors);
        }

        var collected = Collector.Collect(units, output);

        if (collected.IsFailed)
        {
            return Fail("the collection", collected.Errors);
        }

        var manifest = Manifest.Write(
            output, names, seeds, collected.Value, started, DateTimeOffset.UtcNow);

        foreach (var (file, rows) in collected.Value.Rows.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            Console.Out.WriteLine(Invariant($"dataset: {rows} measured rows in {Path.Combine(collected.Value.Directory, file)}"));
        }

        Console.Out.WriteLine(Invariant($"manifest: {manifest}"));
        var elapsed = (DateTimeOffset.UtcNow - started).TotalSeconds;

        Console.Out.WriteLine(Invariant(
            $"done in {elapsed:0.0}s. Nothing here is a result yet — this is the dataset the analysis reads."));

        return 0;
    }

    private static void Report(int done, int total)
    {
        Console.Out.Write(Invariant($"\rruns: {done}/{total}"));
        Console.Out.Flush();
    }

    private static string? Argument(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);

        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static int Fail(string what, IEnumerable<IError> errors)
    {
        Console.Error.WriteLine(Invariant($"campaign: {what} would not do."));

        foreach (var error in errors.Take(20))
        {
            Console.Error.WriteLine("  " + error.Message);
        }

        return 1;
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: dotnet run --project tools/Campaign -- --all [--into <directory>]");
        Console.Error.WriteLine("                                              [--calibration <file>]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Runs every scenario in config/scenarios over seeds 1..run.seeds, one");
        Console.Error.WriteLine("  process each, and collects the measured window of every run into one");
        Console.Error.WriteLine("  dataset with a manifest. Warm-up rows stay in the per-run files.");
        Console.Error.WriteLine("  --calibration runs every arm on a goods table other than the schema");
        Console.Error.WriteLine("  defaults, e.g. config/calibrations/grouped.toml. It is the town, not");
        Console.Error.WriteLine("  an arm: one campaign, one calibration.");

        return 2;
    }
}
