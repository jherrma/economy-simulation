using System.Globalization;

namespace EconomySimulation.Gates;

/// <summary>What a column of the engine's output measures, read off the way the writer formats it.</summary>
public enum Quantity
{
    /// <summary>A label. The scenario name, a category, a tier.</summary>
    Text,

    /// <summary>A count of things: units, households, ticks. Real, and exact.</summary>
    Count,

    /// <summary>A pure number made of counts: a mix share, a quality index, a median wait. Real, and exact.</summary>
    Dimensionless,

    /// <summary>
    /// A pure number made of **money**: the CPI and the per-category indices.
    ///
    /// Dimensionless, so no scale factor should move it — but it is a ratio of cent-rounded prices,
    /// so it is only as precise as the cents it is made of. It is the one kind of column that is
    /// real in economics and approximate in arithmetic, which is why it has a name of its own.
    /// </summary>
    PriceIndex,

    /// <summary>An amount in euros. **The only kind that scales** under V3.</summary>
    Money,
}

/// <summary>
/// One of the engine's CSV files, read back column by column.
///
/// The kind of each value is taken from **how the writer formatted it**, not from a list of column
/// names: money carries exactly two decimals, a dimensionless number exactly six, a count none
/// (07-01). That is not a trick — it is the one classification that cannot go stale. A gate keyed
/// by name would silently stop checking every column added after it was written, which for a
/// deliberately additive schema means it stops checking the new work first.
/// </summary>
public sealed class OutputFile
{
    private readonly List<string[]> rows = [];

    private OutputFile(string name, string[] columns)
    {
        Name = name;
        Columns = columns;
    }

    public string Name { get; }

    public IReadOnlyList<string> Columns { get; }

    public int RowCount => rows.Count;

    public static OutputFile Read(string directory, string file)
    {
        var lines = File.ReadAllLines(Path.Combine(directory, file));

        if (lines.Length == 0)
        {
            throw new InvalidOperationException($"{file} in {directory} is empty; a run writes a header before anything else.");
        }

        var read = new OutputFile(file, lines[0].Split(','));

        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                read.rows.Add(lines[i].Split(','));
            }
        }

        return read;
    }

    public string Field(int row, int column) => rows[row][column];

    /// <summary>Where a named column sits. Keyed by name because the schema is additive: a reader keyed by position breaks the first time a column is inserted.</summary>
    public int Column(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        for (var i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new KeyNotFoundException($"no column '{name}' in {Name}");
    }

    /// <summary>The line number this row has in the file, so a report points at something a person can open.</summary>
    public static int Line(int row) => row + 2;

    /// <summary>
    /// Two decimals is money, six is a pure number, no decimal point is a count or a label. The
    /// writer's formatting is the schema here.
    /// </summary>
    public static Quantity KindOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var point = value.IndexOf('.', StringComparison.Ordinal);

        if (point < 0)
        {
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? Quantity.Count
                : Quantity.Text;
        }

        return value.Length - point - 1 == 2 ? Quantity.Money : Quantity.Dimensionless;
    }

    /// <summary>
    /// Whether a column is a price index.
    ///
    /// By name, and this is the one classification in the gate that can go stale — every other kind
    /// is read off the writer's formatting. It is by name because nothing in the *format* of
    /// `0.986899` says whether the number underneath it was made of counts or of prices, and the
    /// difference decides whether it must be exact or only accurate to the cent. The engine's price
    /// index lives in one file and is written into columns called `cpi` and `cpi_&lt;category&gt;`;
    /// PriceIndexTests holds it there.
    /// </summary>
    public static bool IsPriceIndex(string column)
    {
        ArgumentNullException.ThrowIfNull(column);

        return string.Equals(column, "cpi", StringComparison.Ordinal)
            || column.StartsWith("cpi_", StringComparison.Ordinal);
    }

    /// <summary>Any numeric field, as a double. Money included: a mean of amounts is not itself an amount.</summary>
    public static double Number(string value) =>
        double.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>Whether a column exists at all — the schema is additive, so an older file may not have one.</summary>
    public bool Has(string column)
    {
        ArgumentNullException.ThrowIfNull(column);

        for (var i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i], column, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>An amount of money in cents, exactly — the value went out as a decimal and comes back as one.</summary>
    public static long Cents(string value) =>
        (long)(decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture) * 100m);
}
