using EconomySimulation.Engine.Configuration;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// Not a gate. A power probe: it runs the two arms of the experiment and asks whether the
/// campaign, at the seed count it is specified with, can resolve the difference between them.
///
/// It exists because §10.2 made the question live. A run's trajectory is chaotic, so the paired
/// difference between two arms carries the trajectory noise of both, and no amount of pairing
/// removes it. Whether the headline is measurable is therefore an empirical question about
/// effect size against seed spread, and this answers it before the campaign is built rather
/// than after it produces a number nobody can defend.
/// </summary>
public static class PilotProbe
{
    /// <summary>A quantity the write-up would report, reduced to one number per run.</summary>
    private sealed record Measure(string Name, Func<Reading, double> Of);

    /// <summary>One run's window, summed rather than averaged where the quantity is a ratio.</summary>
    private sealed class Reading
    {
        private readonly Dictionary<string, double> sums = [];
        private int ticks;

        public double Sum(string column) => sums.TryGetValue(column, out var value) ? value : 0.0;

        public double Mean(string column) => ticks == 0 ? 0.0 : Sum(column) / ticks;

        public double Share(string numerator, string denominator)
        {
            var below = Sum(denominator);

            return below == 0.0 ? double.NaN : Sum(numerator) / below;
        }

        public double Share(IEnumerable<string> numerator, IEnumerable<string> denominator)
        {
            var below = denominator.Sum(Sum);

            return below == 0.0 ? double.NaN : numerator.Sum(Sum) / below;
        }

        public static Reading Of(string directory)
        {
            var reading = new Reading();
            var read = OutputFile.Read(directory, "run.csv");
            var warmup = read.Column("warmup");

            for (var row = 0; row < read.RowCount; row++)
            {
                if (!string.Equals(read.Field(row, warmup), "0", StringComparison.Ordinal))
                {
                    continue;
                }

                reading.ticks++;

                for (var column = 0; column < read.Columns.Count; column++)
                {
                    var name = read.Columns[column];
                    var text = read.Field(row, column);

                    if (OutputFile.KindOf(text) == Quantity.Text && !OutputFile.IsPriceIndex(name))
                    {
                        continue;
                    }

                    reading.sums.TryGetValue(name, out var running);
                    reading.sums[name] = running + OutputFile.Number(text);
                }
            }

            return reading;
        }
    }

    private static readonly string[] Categories =
        ["food", "leisure", "clothing", "hobby", "electronics", "appliances"];

    private static IEnumerable<string> Units(string cohort, string tier) =>
        Categories.Select(c => Invariant($"{cohort}_{c}_{tier}_units"));

    private static IEnumerable<string> AllUnits(string cohort) =>
        new[] { "budget", "standard", "premium" }.SelectMany(t => Units(cohort, t));

    private static Measure[] Measures()
    {
        var measures = new List<Measure>
        {
            new("cpi", r => r.Mean("cpi")),
            new("abstainer share obtained", r => r.Share("abstainer_obtained", "abstainer_wanted")),
            new("abstainer units / tick", r => r.Mean("abstainer_obtained")),
            new("abstainer quality", r => r.Mean("abstainer_quality")),
            new("abstainer spend / tick", r => r.Mean("abstainer_spend")),
            new("abstainer cash", r => r.Mean("abstainer_cash")),
            new("abstainer wait_median", r => r.Mean("abstainer_wait_median")),
            new("abstainer budget share", r => r.Share(Units("abstainer", "budget"), AllUnits("abstainer"))),
            new("abstainer premium share", r => r.Share(Units("abstainer", "premium"), AllUnits("abstainer"))),
        };

        measures.AddRange(Categories.Select(c => new Measure(
            Invariant($"  abstainer {c} obtained"),
            r => r.Share(Invariant($"abstainer_{c}_obtained"), Invariant($"abstainer_{c}_wanted")))));

        measures.AddRange(Categories.Select(c => new Measure(
            Invariant($"  cpi_{c}"),
            r => r.Mean(Invariant($"cpi_{c}")))));

        measures.AddRange(
        [
            new("borrower share obtained", r => r.Share("borrower_obtained", "borrower_wanted")),
            new("borrower premium share", r => r.Share(Units("borrower", "premium"), AllUnits("borrower"))),
            new("loans_outstanding", r => r.Mean("loans_outstanding")),
            new("money_stock", r => r.Mean("money_stock")),
            new("rationed / tick", r => r.Mean("rationed")),
        ]);

        return [.. measures];
    }

    public static GateReport Run(SimulationParameters parameters, IReadOnlyList<int> seeds, Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(workspace);

        var report = new GateReport("pilot — can the campaign resolve the difference it is for?");
        var arms = report.Check(Invariant($"both arms complete on {seeds.Count} seeds"));

        var off = workspace.Arm("credit_off");
        var high = workspace.Arm("credit_high");

        var ranOff = Runs.Execute(Scenarios.CreditOff(parameters), seeds, off, threaded: true);
        var ranHigh = Runs.Execute(Scenarios.CreditHigh(parameters), seeds, high, threaded: true);

        if (ranOff.IsFailed || ranHigh.IsFailed)
        {
            arms.Fail(string.Join("; ", ranOff.Errors.Concat(ranHigh.Errors).Select(e => e.Message)));

            return report;
        }

        var offReadings = seeds.Select(s => Reading.Of(Runs.Directory(off, s))).ToList();
        var highReadings = seeds.Select(s => Reading.Of(Runs.Directory(high, s))).ToList();

        arms.Observe(Invariant($"{seeds.Count} paired runs at {parameters.Run.Ticks} ticks, window from {parameters.Run.WarmupTicks + 1}"));

        var resolution = report.Check("what the campaign can resolve, per measure");

        resolution.Observe("measure | credit_off | credit_high | diff | diff % | paired sd % | t | MDE % | unpaired MDE %");

        foreach (var measure in Measures())
        {
            var left = offReadings.Select(measure.Of).ToList();
            var right = highReadings.Select(measure.Of).ToList();

            if (left.Exists(double.IsNaN) || right.Exists(double.IsNaN))
            {
                resolution.Observe(Invariant($"{measure.Name} | not defined in one arm"));
                continue;
            }

            var level = Average(left);
            var differences = left.Zip(right, (l, r) => r - l).ToList();
            var difference = Average(differences);
            var spread = Spread(differences);
            var unpaired = Math.Sqrt(Variance(left) + Variance(right));
            var error = spread / Math.Sqrt(seeds.Count);
            var scale = Math.Abs(level) < 1e-12 ? double.NaN : Math.Abs(level);

            // Two-sided 95% on a paired t with n - 1 degrees of freedom, near enough at n = 30.
            const double Critical = 2.045;

            var treated = Average(right);
            var relative = 100.0 * difference / scale;
            var noise = 100.0 * spread / scale;
            var t = error == 0.0 ? 0.0 : difference / error;
            var detectable = 100.0 * Critical * error / scale;
            var alone = 100.0 * Critical * unpaired / Math.Sqrt(seeds.Count) / scale;

            resolution.Observe(Invariant($"{measure.Name} | {level:0.####} | {treated:0.####} | {difference:+0.####;-0.####;0} | {relative:+0.000;-0.000;0.000}% | {noise:0.000}% | {t:0.0} | {detectable:0.000}% | {alone:0.000}%"));
        }

        return report;
    }

    private static double Average(IReadOnlyList<double> values) => values.Sum() / values.Count;

    private static double Variance(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = Average(values);

        return values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
    }

    private static double Spread(IReadOnlyList<double> values) => Math.Sqrt(Variance(values));
}
