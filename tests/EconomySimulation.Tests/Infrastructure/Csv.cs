using System.Globalization;

namespace EconomySimulation.Tests.Infrastructure;

/// <summary>
/// A reader for the engine's output, keyed **by column name**.
///
/// Keyed by name on purpose: the schema is additive, so a reader that took column 7 would break the
/// first time a column was inserted, and the whole point of the additive rule is that older files
/// and newer readers keep working together. This is the shape the analysis outside the engine is
/// expected to use, so the schema test uses it too.
/// </summary>
internal sealed class Csv
{
    private readonly string[] columns;
    private readonly List<string[]> rows = [];

    private Csv(string[] columns) => this.columns = columns;

    internal int RowCount => rows.Count;

    internal IReadOnlyList<string> Columns => columns;

    internal static Csv Read(string path)
    {
        var lines = File.ReadAllLines(path);
        var csv = new Csv(lines[0].Split(','));

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                csv.rows.Add(lines[i].Split(','));
            }
        }

        return csv;
    }

    internal bool Has(string column) => Array.IndexOf(columns, column) >= 0;

    internal string Text(int row, string column)
    {
        var at = Array.IndexOf(columns, column);

        if (at < 0)
        {
            throw new KeyNotFoundException($"no column '{column}' in [{string.Join(", ", columns)}]");
        }

        return rows[row][at];
    }

    internal int Integer(int row, string column) =>
        int.Parse(Text(row, column), CultureInfo.InvariantCulture);

    internal double Number(int row, string column) =>
        double.Parse(Text(row, column), CultureInfo.InvariantCulture);

    internal IEnumerable<int> Rows() => Enumerable.Range(0, rows.Count);

    internal IEnumerable<int> Where(string column, string value) =>
        Rows().Where(r => string.Equals(Text(r, column), value, StringComparison.Ordinal));
}
