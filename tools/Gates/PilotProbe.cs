using EconomySimulation.Engine.Configuration;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// Not a gate. A power probe: it runs the two arms of the experiment and asks whether the
/// campaign, at the seed count it is specified with, can resolve the difference between them.
///
/// **It takes a table.** Since E10 the population is a table of archetypes, and the sweep grid of
/// `02-PARAMETERS.md` §3.5 has five of them. Each is a different baseline economy, so each has its
/// own control arm and its own answer to "can thirty seeds see this" — archetypes add dispersion in
/// exactly the place that decides who is marginal in the financeable categories, and pairing does
/// not rescue that: the same household being the same type in both arms cancels the *draw*, not the
/// trajectory the draw sets off. So the honest order is to measure the power first and read the
/// finding second, because a difference read off an underpowered comparison looks exactly like a
/// difference read off a powered one.
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

    /// <summary>
    /// The goods and tiers to cut the probe by, **read off the table being run**.
    ///
    /// This was six names and three written out by hand, which was true of every configuration that
    /// existed and stopped being true the moment §3.6 shipped eighteen goods. A hard-coded list does
    /// not fail when it goes stale — it silently sums a share over the six columns it knows and
    /// reports a denominator missing two thirds of the town.
    /// </summary>
    private sealed record Cuts(IReadOnlyList<string> Goods, IReadOnlyList<string> Tiers)
    {
        public static Cuts Of(SimulationParameters parameters) =>
            new(
                [.. parameters.Categories.Select(c => c.Name)],
                [.. parameters.Tiers.Select(t => t.Name)]);

        public IEnumerable<string> Units(string cohort, string tier) =>
            Goods.Select(g => Invariant($"{cohort}_{g}_{tier}_units"));

        public IEnumerable<string> AllUnits(string cohort) =>
            Tiers.SelectMany(t => Units(cohort, t));
    }

    private static Measure[] Measures(Cuts cuts)
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
            new("abstainer budget share", r => r.Share(cuts.Units("abstainer", "budget"), cuts.AllUnits("abstainer"))),
            new("abstainer premium share", r => r.Share(cuts.Units("abstainer", "premium"), cuts.AllUnits("abstainer"))),
        };

        measures.AddRange(cuts.Goods.Select(g => new Measure(
            Invariant($"  abstainer {g} obtained"),
            r => r.Share(Invariant($"abstainer_{g}_obtained"), Invariant($"abstainer_{g}_wanted")))));

        measures.AddRange(cuts.Goods.Select(g => new Measure(
            Invariant($"  cpi_{g}"),
            r => r.Mean(Invariant($"cpi_{g}")))));

        measures.AddRange(
        [
            new("borrower share obtained", r => r.Share("borrower_obtained", "borrower_wanted")),
            new("borrower premium share", r => r.Share(cuts.Units("borrower", "premium"), cuts.AllUnits("borrower"))),
            new("loans_outstanding", r => r.Mean("loans_outstanding")),
            new("money_stock", r => r.Mean("money_stock")),
            new("rationed / tick", r => r.Mean("rationed")),
        ]);

        return [.. measures];
    }

    /// <summary>The row of §3.5's sweep grid this probe runs by default: v1's population.</summary>
    public const string IdentityTable = "identity";

    public static GateReport Run(
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        Workspace workspace,
        string table = IdentityTable)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(workspace);

        var report = new GateReport(Invariant($"pilot — can the campaign resolve the difference it is for? ({table})"));
        var arms = report.Check(Invariant($"both arms complete on {seeds.Count} seeds"));

        var row = Scenario.SweepGrid.FirstOrDefault(r => string.Equals(r.Table, table, StringComparison.Ordinal));

        if (row.Scenarios is null)
        {
            arms.Fail(Invariant(
                $"no table called '{table}' in the sweep grid; it has {string.Join(", ", Scenario.SweepGrid.Select(r => r.Table))}"));

            return report;
        }

        var off = workspace.Arm(row.Scenarios[0]);
        var high = workspace.Arm(row.Scenarios[1]);

        var control = Scenarios.Apply(row.Scenarios[0], parameters);
        var treatment = Scenarios.Apply(row.Scenarios[1], parameters);

        // The two arms have to carry the same table, or the difference between them is the table
        // and the credit together and this probe would be measuring the wrong thing precisely.
        if (control.Archetypes != treatment.Archetypes)
        {
            arms.Fail(Invariant(
                $"{row.Scenarios[0]} and {row.Scenarios[1]} carry different archetype tables; the difference between them would not be credit"));

            return report;
        }

        var ranOff = Runs.Execute(control, seeds, off, threaded: true);
        var ranHigh = Runs.Execute(treatment, seeds, high, threaded: true);

        if (ranOff.IsFailed || ranHigh.IsFailed)
        {
            arms.Fail(string.Join("; ", ranOff.Errors.Concat(ranHigh.Errors).Select(e => e.Message)));

            return report;
        }

        var offReadings = seeds.Select(s => Reading.Of(Runs.Directory(off, s))).ToList();
        var highReadings = seeds.Select(s => Reading.Of(Runs.Directory(high, s))).ToList();

        arms.Observe(Invariant($"{seeds.Count} paired runs at {parameters.Run.Ticks} ticks, window from {parameters.Run.WarmupTicks + 1}"));
        arms.Observe(Invariant($"table '{table}': {row.Scenarios[0]} against {row.Scenarios[1]}, {control.Archetypes.Types.Count} archetype(s)"));

        var resolution = report.Check("what the campaign can resolve, per measure");

        resolution.Observe(Invariant($"measure | {row.Scenarios[0]} | {row.Scenarios[1]} | diff | diff % | paired sd % | t | MDE % | unpaired MDE %"));

        foreach (var measure in Measures(Cuts.Of(control)))
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
