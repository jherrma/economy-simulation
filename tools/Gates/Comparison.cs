using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>Where two runs first stopped agreeing, in terms a person can go and look at.</summary>
/// <param name="File">The output file, relative to a run's directory.</param>
/// <param name="Row">The line number in that file, counting the header as line 1.</param>
/// <param name="Column">The column's name from the header, or a description when there is no header to name it.</param>
public sealed record Difference(string File, int Row, string Column, string Left, string Right)
{
    public override string ToString() =>
        Invariant($"{File}, line {Row}, column '{Column}': {Left} != {Right}");
}

/// <summary>
/// Compares two runs' output directories and reports where they first differ.
///
/// **The report is the feature.** A comparison that returns a bare "not equal" is a comparison
/// nobody can act on: the difference between two 360-tick runs is a hundred thousand numbers, and
/// finding which one moved by hand is the work this exists to avoid. Naming the file, the line and
/// the column turns a failed gate into a place to put a breakpoint.
///
/// The comparison is byte for byte and there is no tolerance anywhere in it. Every gate that uses
/// it is asserting that two runs are the *same run*, and a tolerance would let a real difference
/// hide underneath it — which is exactly the failure V2 exists to catch, because a model that has
/// become order-dependent drifts slowly at first.
/// </summary>
public static class Comparison
{
    /// <summary>
    /// What a run leaves behind, in the order a difference is most usefully reported: the
    /// aggregates first, the eighteen shelves second, the marker last.
    /// </summary>
    public static IReadOnlyList<string> OutputFiles { get; } = ["run.csv", "tiers.csv", "run.done"];

    /// <summary>Those, and the effective configuration — for the gates whose two runs are meant to have identical inputs too.</summary>
    public static IReadOnlyList<string> OutputAndConfiguration { get; } =
        ["run.csv", "tiers.csv", "run.done", "effective-config.toml"];

    /// <summary>
    /// The first difference between two run directories, or null when every listed file is
    /// identical byte for byte.
    /// </summary>
    public static Difference? FirstDifference(string left, string right, IReadOnlyList<string> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            var difference = FirstDifference(left, right, file);

            if (difference is not null)
            {
                return difference;
            }
        }

        return null;
    }

    /// <summary>Every seed of two campaigns laid out as <see cref="Runs.Directory"/> names them.</summary>
    public static Difference? FirstDifference(
        string left,
        string right,
        IReadOnlyList<int> seeds,
        IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        foreach (var seed in seeds)
        {
            var difference = FirstDifference(Runs.Directory(left, seed), Runs.Directory(right, seed), files);

            if (difference is not null)
            {
                return difference with { File = Invariant($"seed-{seed}/{difference.File}") };
            }
        }

        return null;
    }

    /// <summary>
    /// The first difference between two CSVs **on the columns their headers share**, with the count
    /// of columns that were left out.
    ///
    /// A narrower comparison than the byte-for-byte one above, and it exists for one situation: a
    /// baseline taken before a mechanism existed cannot carry the columns that mechanism adds. V5a
    /// uses it so that adding a cohort column does not turn a neutrality gate permanently red while
    /// saying nothing about neutrality. Everywhere else, the whole-file comparison is the right one
    /// — a tolerance for *columns* is still a tolerance, and this one is only safe because the files
    /// it applies to are named in a projection the gate prints.
    ///
    /// Columns are matched by name, not by position, and the row count still has to agree.
    /// </summary>
    /// <summary>
    /// The columns <paramref name="left"/> has that <paramref name="right"/> does not — for a gate
    /// comparing on shared columns, the ones whose absence is a finding rather than a version.
    /// </summary>
    public static IReadOnlyList<string> ColumnsMissing(string left, string right, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        var leftPath = Path.Combine(left, file);
        var rightPath = Path.Combine(right, file);

        if (!File.Exists(leftPath) || !File.Exists(rightPath))
        {
            return [];
        }

        var leftHeader = Header(leftPath);
        var rightHeader = Header(rightPath).ToHashSet(StringComparer.Ordinal);

        return [.. leftHeader.Where(c => !rightHeader.Contains(c))];
    }

    private static string[] Header(string path)
    {
        using var reader = new StreamReader(path);

        return reader.ReadLine()?.Split(',') ?? [];
    }

    public static Difference? FirstDifferenceOnSharedColumns(
        string left,
        string right,
        string file,
        out int dropped)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        dropped = 0;

        var leftLines = File.ReadAllLines(Path.Combine(left, file));
        var rightLines = File.ReadAllLines(Path.Combine(right, file));

        if (leftLines.Length == 0 || rightLines.Length == 0)
        {
            return new Difference(
                file,
                0,
                "(the file itself)",
                Invariant($"{leftLines.Length} lines"),
                Invariant($"{rightLines.Length} lines"));
        }

        var leftHeader = leftLines[0].Split(',');
        var rightHeader = rightLines[0].Split(',');

        var shared = new List<(string Name, int Left, int Right)>();

        for (var i = 0; i < leftHeader.Length; i++)
        {
            var j = Array.IndexOf(rightHeader, leftHeader[i]);

            if (j >= 0)
            {
                shared.Add((leftHeader[i], i, j));
            }
        }

        dropped = leftHeader.Length + rightHeader.Length - (2 * shared.Count);

        if (shared.Count == 0)
        {
            return new Difference(file, 1, "(the header)", leftLines[0], rightLines[0]);
        }

        for (var line = 1; line < Math.Max(leftLines.Length, rightLines.Length); line++)
        {
            if (line >= leftLines.Length || line >= rightLines.Length)
            {
                return new Difference(
                    file,
                    line + 1,
                    "(the row itself)",
                    line < leftLines.Length ? leftLines[line] : "(no such line)",
                    line < rightLines.Length ? rightLines[line] : "(no such line)");
            }

            var leftFields = leftLines[line].Split(',');
            var rightFields = rightLines[line].Split(',');

            foreach (var (name, l, r) in shared)
            {
                var leftField = l < leftFields.Length ? leftFields[l] : "(no such field)";
                var rightField = r < rightFields.Length ? rightFields[r] : "(no such field)";

                if (!string.Equals(leftField, rightField, StringComparison.Ordinal))
                {
                    return new Difference(file, line + 1, name, leftField, rightField);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The first difference between two `key = value` files **on the keys the left one states**,
    /// with the count of keys the right one has that it does not.
    ///
    /// For `run.done`, and only for `run.done`. The marker is metadata *about the files*, not model
    /// output: it names how many rows each of them got, so adding an output file necessarily adds a
    /// line to it. V5a compares against a fixture that is never retaken, so a marker line that a
    /// later story adds must not be able to fail it — while `ticks` and `tier_rows`, which are the
    /// marker's actual claim about the run, still have to agree to the character.
    /// </summary>
    public static Difference? FirstDifferenceOnSharedKeys(
        string left,
        string right,
        string file,
        out int dropped)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        var expected = Keyed(Path.Combine(left, file));
        var actual = Keyed(Path.Combine(right, file));

        dropped = actual.Count - expected.Keys.Count(k => actual.ContainsKey(k));

        var line = 0;

        foreach (var (key, value) in expected)
        {
            line++;

            if (!actual.TryGetValue(key, out var got))
            {
                return new Difference(file, line, key, value, "(no such key)");
            }

            if (!string.Equals(value, got, StringComparison.Ordinal))
            {
                return new Difference(file, line, key, value, got);
            }
        }

        return null;
    }

    private static Dictionary<string, string> Keyed(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var raw in File.ReadAllLines(path))
        {
            var separator = raw.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0)
            {
                values[raw[..separator].Trim()] = raw[(separator + 1)..].Trim();
            }
        }

        return values;
    }

    private static Difference? FirstDifference(string left, string right, string file)
    {
        var leftPath = Path.Combine(left, file);
        var rightPath = Path.Combine(right, file);

        if (!File.Exists(leftPath) || !File.Exists(rightPath))
        {
            return new Difference(
                file,
                0,
                "(the file itself)",
                File.Exists(leftPath) ? "present" : "missing",
                File.Exists(rightPath) ? "present" : "missing");
        }

        var leftBytes = File.ReadAllBytes(leftPath);
        var rightBytes = File.ReadAllBytes(rightPath);

        if (leftBytes.AsSpan().SequenceEqual(rightBytes))
        {
            return null;
        }

        var leftLines = File.ReadAllLines(leftPath);
        var rightLines = File.ReadAllLines(rightPath);
        var header = Header(file, leftLines);

        for (var line = 0; line < Math.Max(leftLines.Length, rightLines.Length); line++)
        {
            if (line >= leftLines.Length || line >= rightLines.Length)
            {
                return new Difference(
                    file,
                    line + 1,
                    "(the row itself)",
                    line < leftLines.Length ? leftLines[line] : "(no such line)",
                    line < rightLines.Length ? rightLines[line] : "(no such line)");
            }

            if (string.Equals(leftLines[line], rightLines[line], StringComparison.Ordinal))
            {
                continue;
            }

            return FirstField(file, line, header, leftLines[line], rightLines[line]);
        }

        // Every line matched and the bytes did not: the files differ in line endings or in a final
        // newline. Reported rather than passed — output has to be identical on every machine, and
        // a line ending is exactly the kind of difference a platform introduces silently.
        return new Difference(
            file,
            0,
            "(line endings)",
            Invariant($"{leftBytes.Length} bytes"),
            Invariant($"{rightBytes.Length} bytes"));
    }

    private static Difference FirstField(string file, int line, string[] header, string left, string right)
    {
        var leftFields = left.Split(',');
        var rightFields = right.Split(',');

        for (var field = 0; field < Math.Max(leftFields.Length, rightFields.Length); field++)
        {
            var leftField = field < leftFields.Length ? leftFields[field] : "(no such field)";
            var rightField = field < rightFields.Length ? rightFields[field] : "(no such field)";

            if (!string.Equals(leftField, rightField, StringComparison.Ordinal))
            {
                return new Difference(file, line + 1, Name(header, field, left), leftField, rightField);
            }
        }

        return new Difference(file, line + 1, "(the whole row)", left, right);
    }

    /// <summary>A CSV names its own columns; the marker and the configuration are `key = value`, so the key is the name.</summary>
    private static string[] Header(string file, string[] lines) =>
        file.EndsWith(".csv", StringComparison.Ordinal) && lines.Length > 0 ? lines[0].Split(',') : [];

    /// <summary>The header's name for that column; failing that, the `key` of a `key = value` line; failing that, its position.</summary>
    private static string Name(string[] header, int field, string line)
    {
        if (field < header.Length)
        {
            return header[field];
        }

        var equals = line.IndexOf('=', StringComparison.Ordinal);

        return field == 0 && equals > 0 ? line[..equals].Trim() : Invariant($"(column {field + 1}, unnamed)");
    }
}
