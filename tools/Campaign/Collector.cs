using EconomySimulation.Engine.Output;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Campaign;

/// <summary>
/// One dataset out of 150 runs, and the refusals that stand between them.
///
/// The marker is the criterion that protects everything upstream. A run that halted on V1 may still
/// have left a plausible partial CSV behind, and a collector that treats a file's existence as
/// success will fold it into the average and produce a number that is wrong for a reason nobody
/// will ever find.
/// </summary>
public static class Collector
{
    public static IReadOnlyList<string> Files { get; } = ["run.csv", "tiers.csv"];

    /// <summary>
    /// Concatenates the measured window of every run, keyed by the scenario and seed the rows
    /// already carry.
    ///
    /// The warm-up rows stay in the per-run files and stay out of the dataset. They are evidence
    /// that the transient decayed (V4) and they are not observations of the economy the question is
    /// about, and one file cannot be both.
    /// </summary>
    public static Result<Collected> Collect(IReadOnlyList<Fleet.Unit> units, string output)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        var missing = new List<IError>();

        foreach (var unit in units)
        {
            var marker = Path.Combine(Layout.Run(output, unit.Scenario, unit.Seed), MetricsWriter.MarkerFile);

            if (!File.Exists(marker))
            {
                var directory = Layout.Run(output, unit.Scenario, unit.Seed);

                missing.Add(new Error(Invariant(
                    $"{unit}: no {MetricsWriter.MarkerFile} in {directory} — the run did not finish, and a partial run is not a smaller run")));
            }
        }

        if (missing.Count > 0)
        {
            return Result.Fail<Collected>(missing);
        }

        var dataset = Layout.Dataset(output);
        Directory.CreateDirectory(dataset);

        var rows = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in Files)
        {
            var written = Concatenate(units, output, file, Path.Combine(dataset, file));

            if (written.IsFailed)
            {
                return written.ToResult<Collected>();
            }

            rows[file] = written.Value;
        }

        var configurations = Configurations(units, output);

        return configurations.IsFailed
            ? configurations.ToResult<Collected>()
            : Result.Ok(new Collected(dataset, rows, configurations.Value));
    }

    /// <summary>The dataset, what went into it, and one effective configuration per scenario.</summary>
    public sealed record Collected(
        string Directory,
        IReadOnlyDictionary<string, int> Rows,
        IReadOnlyDictionary<string, string> Configurations);

    private static Result<int> Concatenate(
        IReadOnlyList<Fleet.Unit> units,
        string output,
        string file,
        string into)
    {
        string? header = null;
        var written = 0;

        using var writer = new StreamWriter(into);

        foreach (var unit in units)
        {
            var path = Path.Combine(Layout.Run(output, unit.Scenario, unit.Seed), file);
            var lines = File.ReadAllLines(path);

            if (lines.Length == 0)
            {
                return Result.Fail<int>(Invariant($"{unit}: {file} is empty behind a completion marker"));
            }

            if (header is null)
            {
                header = lines[0];
                writer.WriteLine(header);
            }
            else if (!string.Equals(header, lines[0], StringComparison.Ordinal))
            {
                // Two runs of one campaign with different columns cannot be concatenated into
                // anything, and appending them anyway would shift every field silently.
                return Result.Fail<int>(Invariant(
                    $"{unit}: {file} has a different header from the first run of this campaign"));
            }

            var warmup = Array.IndexOf(header.Split(','), "warmup");

            if (warmup < 0)
            {
                return Result.Fail<int>(Invariant($"{file}: no 'warmup' column, so the measured window cannot be found"));
            }

            foreach (var line in lines.Skip(1))
            {
                if (string.Equals(Field(line, warmup), "0", StringComparison.Ordinal))
                {
                    writer.WriteLine(line);
                    written++;
                }
            }
        }

        return Result.Ok(written);
    }

    /// <summary>
    /// One effective configuration per scenario, and a refusal if the seeds of one scenario
    /// disagree about it. The seed is not a parameter; if it has become one, the campaign is
    /// comparing scenarios that differ in more than their scenario.
    /// </summary>
    private static Result<IReadOnlyDictionary<string, string>> Configurations(
        IReadOnlyList<Fleet.Unit> units,
        string output)
    {
        var byScenario = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<IError>();

        foreach (var unit in units)
        {
            var path = Path.Combine(Layout.Run(output, unit.Scenario, unit.Seed), "effective-config.toml");
            var text = File.ReadAllText(path);

            if (!byScenario.TryGetValue(unit.Scenario, out var first))
            {
                byScenario[unit.Scenario] = text;
            }
            else if (!string.Equals(first, text, StringComparison.Ordinal))
            {
                problems.Add(new Error(Invariant(
                    $"{unit}: a different effective configuration from the first seed of {unit.Scenario}")));
            }
        }

        return problems.Count > 0
            ? Result.Fail<IReadOnlyDictionary<string, string>>(problems)
            : Result.Ok<IReadOnlyDictionary<string, string>>(byScenario);
    }

    private static string Field(string line, int index)
    {
        var start = 0;

        for (var field = 0; field < index; field++)
        {
            start = line.IndexOf(',', start) + 1;

            if (start == 0)
            {
                return string.Empty;
            }
        }

        var end = line.IndexOf(',', start);

        return end < 0 ? line[start..] : line[start..end];
    }
}
