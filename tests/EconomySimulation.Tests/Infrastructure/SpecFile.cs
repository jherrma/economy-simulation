using System.Globalization;

namespace EconomySimulation.Tests.Infrastructure;

/// <summary>
/// Reads the tables out of `spec/02-PARAMETERS.md`, so that the schema's defaults can be compared
/// against the specification itself rather than against a second copy of it in a test.
///
/// This is the mechanism behind "a parameter not in 02-PARAMETERS.md does not exist". Without it
/// the rule is a convention, and the way it fails is that a number gets tuned in the code and the
/// file quietly stops describing the model that produced the results.
/// </summary>
internal static class SpecFile
{
    private static readonly string[] Lines =
        File.ReadAllLines(Path.Combine(Repo.Root, "spec", "02-PARAMETERS.md"));

    /// <summary>Every table under a heading, in order, each as a list of already-split rows.</summary>
    internal static IReadOnlyList<IReadOnlyList<string[]>> Tables(string heading)
    {
        var start = Array.FindIndex(Lines, l => l.StartsWith(heading, StringComparison.Ordinal));

        if (start < 0)
        {
            throw new InvalidOperationException(
                $"spec/02-PARAMETERS.md has no heading starting '{heading}'. The specification has "
                + "been restructured and these tests need to follow it.");
        }

        var tables = new List<IReadOnlyList<string[]>>();
        var current = new List<string[]>();

        for (var i = start + 1; i < Lines.Length; i++)
        {
            var line = Lines[i].Trim();

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith('|'))
            {
                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();

                // Skip the header separator row.
                if (!cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':')))
                {
                    current.Add(cells);
                }

                continue;
            }

            if (current.Count > 0)
            {
                tables.Add(current);
                current = [];
            }
        }

        if (current.Count > 0)
        {
            tables.Add(current);
        }

        return tables;
    }

    /// <summary>The first table under a heading, keyed by its first column, minus the header row.</summary>
    internal static IReadOnlyDictionary<string, string[]> Table(string heading)
    {
        var rows = Tables(heading)[0];

        return rows
            .Skip(1)
            .ToDictionary(r => Clean(r[0]), r => r, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The default column of a parameter table: key to value, both cleaned.</summary>
    internal static IReadOnlyDictionary<string, string> Defaults(string heading) =>
        Table(heading).ToDictionary(e => e.Key, e => Clean(e.Value[1]), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Strips the markdown the specification is written in — emphasis, code ticks, the currency
    /// symbol, per-annum suffixes, thousands separators — and leaves the value.
    /// </summary>
    internal static string Clean(string cell)
    {
        var text = cell
            .Replace("**", "", StringComparison.Ordinal)
            .Replace("`", "", StringComparison.Ordinal)
            .Replace(" ", " ", StringComparison.Ordinal)
            .Replace("€", "", StringComparison.Ordinal)
            .Replace("%/a", "", StringComparison.Ordinal)
            .Replace("%", "", StringComparison.Ordinal)
            .Trim();

        // Thousands separators, but only between digits: 8,450,000 is a number, "0, 0.2" is not.
        if (text.Length > 0 && text.All(c => char.IsDigit(c) || c is ',' or '.'))
        {
            text = text.Replace(",", "", StringComparison.Ordinal);
        }

        // The parameter tables spell a parameter's name in the first column, sometimes with the
        // symbol it is written as in the formulas: "lambda (λ)".
        var parenthesis = text.IndexOf(" (", StringComparison.Ordinal);
        if (parenthesis > 0 && text.EndsWith(')'))
        {
            text = text[..parenthesis];
        }

        return text.Trim();
    }

    internal static double Number(string cell) =>
        double.Parse(Clean(cell), CultureInfo.InvariantCulture);

    internal static int Integer(string cell) =>
        int.Parse(Clean(cell), CultureInfo.InvariantCulture);

    internal static bool Flag(string cell) => Clean(cell).ToLowerInvariant() switch
    {
        "yes" or "true" => true,
        "no" or "false" => false,
        var other => throw new InvalidOperationException($"'{other}' is neither yes nor no."),
    };
}
