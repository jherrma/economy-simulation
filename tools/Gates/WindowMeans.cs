namespace EconomySimulation.Gates;

/// <summary>
/// The mean of every series of one run over its **measured window** — the ticks the run itself
/// flags as past the warm-up.
///
/// The window rather than the whole run because the warm-up is a transient by design: the opening
/// tier mix is deliberately not an equilibrium, and averaging the approach to it in with the
/// equilibrium would compare two transients rather than two economies.
/// </summary>
public sealed class WindowMeans
{
    private readonly Dictionary<Series, double> means = [];
    private readonly Dictionary<Series, Quantity> kinds = [];

    private WindowMeans()
    {
    }

    /// <summary>One measured series: which file it is in and what it is called.</summary>
    public readonly record struct Series(string File, string Column)
    {
        public override string ToString() => $"{File}, column '{Column}'";
    }

    public IEnumerable<Series> Measured => means.Keys;

    public double Mean(Series series) => means[series];

    public Quantity Kind(Series series) => kinds[series];

    public bool Has(Series series) => means.ContainsKey(series);

    /// <summary>
    /// Reads a run's output and averages every non-label column over the measured window.
    ///
    /// `tiers.csv` carries eighteen rows a tick, one per shelf, so its columns are averaged **per
    /// shelf**: `price` on its own would mix six categories and three tiers into a number with no
    /// meaning. The category and tier become part of the series name instead.
    /// </summary>
    public static WindowMeans Of(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var measured = new WindowMeans();
        var totals = new Dictionary<Series, (double Sum, int Count)>();

        foreach (var file in new[] { "run.csv", "tiers.csv" })
        {
            var read = OutputFile.Read(directory, file);
            var warmup = read.Column("warmup");
            var shelf = read.Has("category") && read.Has("tier");

            for (var row = 0; row < read.RowCount; row++)
            {
                if (!string.Equals(read.Field(row, warmup), "0", StringComparison.Ordinal))
                {
                    continue;
                }

                var prefix = shelf
                    ? read.Field(row, read.Column("category")) + "." + read.Field(row, read.Column("tier")) + "."
                    : string.Empty;

                for (var column = 0; column < read.Columns.Count; column++)
                {
                    var name = read.Columns[column];
                    var kind = OutputFile.IsPriceIndex(name) ? Quantity.PriceIndex : OutputFile.KindOf(read.Field(row, column));

                    if (kind == Quantity.Text)
                    {
                        continue;
                    }

                    var series = new Series(file, prefix + name);
                    var value = OutputFile.Number(read.Field(row, column));

                    totals.TryGetValue(series, out var running);
                    totals[series] = (running.Sum + value, running.Count + 1);
                    measured.kinds[series] = kind;
                }
            }
        }

        foreach (var (series, running) in totals)
        {
            measured.means[series] = running.Sum / running.Count;
        }

        return measured;
    }
}
