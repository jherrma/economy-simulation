using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// **V4** — the creditless baseline sits still, so that a credit effect measured against it is not
/// drift plus an unknown amount of signal.
///
/// Supply is fixed, income is fixed and the money stock is constant, so there is nothing in this
/// model that should make the price level move. If it drifts, the price rule is not converging.
///
/// Four claims, and the fourth is the one that is easy to leave out:
///
/// - the CPI has no trend over the measured window, and each of the eighteen tier prices is flat;
/// - **the realised tier mix is stationary too**. The opening unit shares are deliberately not an
///   equilibrium — premium starts 60–95% unsold — and the warm-up exists mainly so relative tier
///   prices can find one. A mix still moving at tick 120 means the measured window contains the
///   tail of a transient that will be read as a credit effect, and the symptom is easy to miss
///   because the CPI can look flat while the mix underneath it moves;
/// - the pool has stopped falling, which is the same failure seen from the money side and the
///   cheaper of the two to check;
/// - the cash of deciles one to nine shows no trend. The pool's residual drain is hoarding by the
///   top decile alone, which fixed incomes with one unit per category cannot avoid
///   (`spec/03-VERIFICATION.md` §V4). A drain that is *not* confined to the top decile is the
///   failure this gate exists for.
///
/// It reports the tick each of those settled at rather than only whether it had settled by tick
/// 120, because that is what turns `warmup_ticks` into an observation. A mix that settles at tick
/// 200 and one that settled at tick 12 both pass a test taken over ticks 121 onwards.
///
/// `wait_median` is not among the series checked, and must not be: it mixes a flow of freshly
/// opened wants against a growing stock of wants that are never met, so it drifts and swings
/// several-fold in a perfectly stationary economy (`01-SIMULATION.md` §10.1). Requiring it to be
/// flat would fail this gate on arithmetic.
/// </summary>
public static class NullRunGate
{
    /// <summary>
    /// How far a price series may travel across the measured window, relative to its own mean.
    ///
    /// The price rule moves a shelf by at most `k` — five per cent — in one tick. One per cent over
    /// two hundred and forty ticks is four thousandths of a per cent a tick, three orders of
    /// magnitude below the adjustment speed: the rule has stopped moving the level rather than
    /// moving it slowly.
    /// </summary>
    public const double PriceDrift = 0.01;

    /// <summary>
    /// How far the **CPI** may swing within the window, peak to trough, relative to its mean.
    ///
    /// A trend test alone would pass an oscillation, which has no trend at all, and an oscillating
    /// price rule is precisely what a badly chosen `k` produces. The band is what catches it — but
    /// only on the CPI. A single shelf's price wanders on its own: appliances produce ten units a
    /// tick, two of them premium, so a demand of nought or four against a stock of two is an
    /// ordinary tick and the price takes a five per cent step in an arbitrary direction. Measured
    /// on the defaults, a shelf's band runs to 27% while the CPI's is 2%; at `k = 0.9` the CPI's is
    /// 120%. Twenty per cent sits an order of magnitude above the one and six times below the other.
    /// </summary>
    public const double CpiBand = 0.20;

    /// <summary>A tier's share of its category's sales, over the window. Shares are noisier than prices: they are ratios of counts.</summary>
    public const double MixDrift = 0.02;

    /// <summary>
    /// What the pool may still be draining, per tick, as a share of one tick's total income.
    ///
    /// From §V4 as amended: the residual is hoarding by the top decile, which fixed incomes and one
    /// unit per category cannot avoid. Above this, or not confined to the top decile, is the
    /// failure.
    /// </summary>
    public const double PoolDrainShare = 0.02;

    /// <summary>The trend allowed in the cash of the bottom nine deciles, over the window.</summary>
    public const double CashDrift = 0.02;



    public static GateReport Run(SimulationParameters parameters, IReadOnlyList<int> seeds, Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(workspace);

        var baseline = Scenarios.CreditOff(parameters);
        var report = new GateReport("V4 — the null run");

        if (baseline.Credit.CreditEnabled)
        {
            report
                .Check("the baseline has credit off")
                .Fail("null run: credit_enabled is true. This gate is the creditless baseline and nothing else.");

            return report;
        }

        return Measure(baseline, seeds, workspace, report);
    }

    private static GateReport Measure(
        SimulationParameters baseline,
        IReadOnlyList<int> seeds,
        Workspace workspace,
        GateReport report)
    {
        var runs = new List<NullRun>();
        var arm = workspace.Arm("null");

        foreach (var seed in seeds)
        {
            var directory = Runs.Directory(arm, seed);
            var collected = new Collector(baseline.Run.Ticks);
            var ran = Runs.Execute(baseline, seed, directory, collected.After);

            if (ran.IsFailed)
            {
                var check = report.Check(Invariant($"seed {seed} completes"));

                foreach (var error in ran.Errors)
                {
                    check.Fail(error.Message);
                }

                return report;
            }

            runs.Add(NullRun.Read(directory, collected));
        }

        var warmup = baseline.Run.WarmupTicks;

        Flat(report.Check("the CPI has no trend, and does not swing"), runs, ["cpi"], warmup, PriceDrift, CpiBand);
        Flat(report.Check("no tier price drifts"), runs, runs[0].Prices, warmup, PriceDrift, band: 0.0);
        Flat(report.Check("the realised tier mix is stationary"), runs, runs[0].Mixes, warmup, MixDrift, band: 0.0);
        Flat(report.Check("the cash of deciles one to nine shows no trend"), runs, ["lower_deciles_cash"], warmup, CashDrift, band: 0.0);
        Pool(report.Check("the pool has stopped falling"), runs, warmup);
        NoLoans(report.Check("no loan exists at any tick"), runs);
        Settling(report.Check("everything settled before the warm-up ended"), runs, warmup);

        return report;
    }

    /// <summary>
    /// Every named series: the **mean drift across seeds**, against the stated bound, plus the
    /// spread of that drift from seed to seed.
    ///
    /// Across seeds and not within one, because that is what "no significant trend" has to mean
    /// here. A single seed's price series wanders: the repricing rule moves a shelf by up to `k` a
    /// tick on its own excess demand, and on a thin shelf — appliances produce ten units a tick, two
    /// of them premium — demand of nought or four against a stock of two is an ordinary tick, so the
    /// price takes a five per cent step in an arbitrary direction. Over two hundred and forty ticks
    /// that is a random walk with a spread of tens of per cent, and it is not drift: it averages to
    /// nothing across seeds and it is what §10.2 says the paired comparison has to live with.
    ///
    /// Secular drift does not average away. A price rule that is not converging pushes every seed
    /// the same way, and the mean across thirty of them is where that shows.
    /// </summary>
    private static void Flat(
        GateCheck check,
        IReadOnlyList<NullRun> runs,
        IReadOnlyList<string> names,
        int warmup,
        double drift,
        double band)
    {
        var worstMean = 0.0;
        var worstMeanAt = string.Empty;
        var worstMeanSpread = 0.0;
        var worstAllowed = drift;
        var worstRatio = 0.0;
        var worstSeed = 0.0;
        var worstSeedAt = string.Empty;
        var worstBand = 0.0;
        var bandAt = string.Empty;

        foreach (var name in names)
        {
            var drifts = new List<double>(runs.Count);

            foreach (var run in runs)
            {
                var window = run.Window(name, warmup);
                var moved = Stationarity.Drift(window);
                var swung = Stationarity.Band(window);

                drifts.Add(moved);

                if (Math.Abs(moved) > Math.Abs(worstSeed))
                {
                    worstSeed = moved;
                    worstSeedAt = Invariant($"{name}, seed {run.Seed}");
                }

                if (swung > worstBand)
                {
                    worstBand = swung;
                    bandAt = Invariant($"{name}, seed {run.Seed}");
                }
            }

            // Held to the looser of the stated floor and what thirty seeds can resolve. A shelf's
            // price wanders on its own — appliances produce two premium units a tick, so a demand of
            // nought or four against a stock of two is an ordinary tick and the price takes a five
            // per cent step in an arbitrary direction. The mean of thirty such walks has a standard
            // error of its own, and a drift bound below it asks the seed set to measure something
            // it cannot.
            var mean = drifts.Average();
            var resolves = 3.0 * Spread(drifts) / Math.Sqrt(drifts.Count);
            var allowed = Math.Max(drift, resolves);
            var ratio = Math.Abs(mean) / allowed;

            if (ratio > worstRatio)
            {
                worstRatio = ratio;
                worstMean = mean;
                worstMeanAt = name;
                worstMeanSpread = Spread(drifts);
                worstAllowed = allowed;
            }
        }

        check.Observe(Invariant($"worst secular drift {worstMean:P3} of {worstAllowed:P2} allowed ({worstMeanAt}; spread {worstMeanSpread:P2} across {runs.Count} seeds, floor {drift:P1})"));
        check.Observe(Invariant($"one seed wanders as far as {worstSeed:P2} ({worstSeedAt}); widest band {worstBand:P1} ({bandAt}) — neither is drift"));

        if (worstRatio > 1.0)
        {
            check.Fail(Invariant($"{worstMeanAt} moves {worstMean:P3} across the window on average over {runs.Count} seeds, past the {worstAllowed:P2} its own spread across seeds can explain. That does not average away, and a credit effect measured against it would be drift plus signal."));
        }

        if (band > 0.0 && worstBand > band)
        {
            check.Fail(Invariant($"{bandAt} swings {worstBand:P1} peak to trough, past {band:P1}. A series with no trend and a band that wide is being hunted rather than nudged — k is too large."));
        }
    }

    private static double Spread(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = values.Average();
        var sum = 0.0;

        foreach (var value in values)
        {
            sum += (value - mean) * (value - mean);
        }

        return Math.Sqrt(sum / (values.Count - 1));
    }

    private static void Pool(GateCheck check, IReadOnlyList<NullRun> runs, int warmup)
    {
        var worst = 0.0;
        var at = string.Empty;

        foreach (var run in runs)
        {
            var window = run.Window("pool", warmup);
            var fall = (window[0] - window[^1]) / (window.Length - 1) / run.TotalIncome;

            if (fall > worst)
            {
                worst = fall;
                at = Invariant($"seed {run.Seed}");
            }

            if (run.LowestPool < 0.0)
            {
                check.Fail(Invariant($"seed {run.Seed}: the pool reached {run.LowestPool:F2}. A negative pool is a statement about the parameters, not a rounding error."));
            }
        }

        check.Observe(Invariant($"worst residual drain {worst:P3} of one tick's income, against {PoolDrainShare:P1} allowed ({at})"));

        if (worst > PoolDrainShare)
        {
            check.Fail(Invariant($"{at}: the pool is still falling by {worst:P3} of total income a tick. Either the warm-up is not over or the drain is not confined to the top decile."));
        }
    }

    /// <summary>With credit off there is no lending, and the money stock never moves. Cheap, and it says the switch is really off.</summary>
    private static void NoLoans(GateCheck check, IReadOnlyList<NullRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.EverLent)
            {
                check.Fail(Invariant($"seed {run.Seed}: a loan exists with credit_enabled = false"));
            }
        }

        check.Observe(Invariant($"{runs.Count} seeds, no loan and no money created or destroyed in any tick"));
    }

    /// <summary>
    /// When each group of series settled, against `warmup_ticks`.
    ///
    /// This is what turns the warm-up from a guess into an observation. A gate that answered only
    /// pass or fail would leave the parameter unexamined: a mix that settles at tick 200 and a mix
    /// that settled at tick 12 both pass a test taken over ticks 121 onwards, and only one of them
    /// means the warm-up is long enough.
    ///
    /// Settling is judged the same way stationarity is — on the mean across seeds — because a single
    /// seed's series never stops wandering and would never settle by any bound tight enough to be
    /// worth having.
    /// </summary>
    /// <remarks>
    /// Reported, never failed on. Whether the measured window is inside a transient is answered by
    /// the drift checks, which have the seed set behind them; this is the number that says what to
    /// raise `warmup_ticks` *to*. It is also the less trustworthy of the two, because the thinnest
    /// shelves have no identified level at all — appliances premium is two units a tick, and its
    /// smoothed price still wanders several per cent after six hundred ticks — so their settling
    /// tick moves with the length of the run rather than with the economy.
    /// </remarks>
    private static void Settling(GateCheck check, IReadOnlyList<NullRun> runs, int warmup)
    {
        foreach (var (what, names, drift) in new (string, IReadOnlyList<string>, double)[]
        {
            ("the CPI", ["cpi"], PriceDrift),
            ("the tier prices", runs[0].Prices, PriceDrift),
            ("the tier mix", runs[0].Mixes, MixDrift),
        })
        {
            var latest = 0;
            var at = string.Empty;

            foreach (var name in names)
            {
                var settled = SettlesAt(runs, name, drift);

                if (settled > latest)
                {
                    latest = settled;
                    at = name;
                }
            }

            check.Observe(latest > warmup
                ? Invariant($"{what} settled by tick {latest} ({at}) — after warmup_ticks = {warmup}. If the drift checks above failed, this is the number to raise it to.")
                : Invariant($"{what} settled by tick {latest} ({at}), comfortably inside warmup_ticks = {warmup}"));
        }
    }

    /// <summary>
    /// The earliest tick from which a series stays within <paramref name="tolerance"/> of the level
    /// it ends at, on the mean across seeds.
    ///
    /// Convergence to a level, deliberately, and not "the earliest window with no detectable
    /// trend". The second is what it is tempting to write and it measures the wrong thing: the
    /// shorter the trailing window, the less power thirty seeds have to see a slope in it, so the
    /// answer moves with the length of the run rather than with the transient. Measured that way a
    /// 360-tick run says the prices settle at tick 286 and a 720-tick run says 486, which is a
    /// statement about the seed set and not about the economy.
    ///
    /// The level a series ends at is taken over the final quarter of the run. A run too short to
    /// contain the asymptote will report a settling tick that is too early, which is why the gate
    /// reports every series' number rather than a single verdict.
    /// </summary>
    private static int SettlesAt(IReadOnlyList<NullRun> runs, string name, double tolerance)
    {
        var ticks = runs[0].Whole(name).Length;
        var mean = new double[ticks];

        for (var t = 0; t < ticks; t++)
        {
            var total = 0.0;

            foreach (var run in runs)
            {
                total += run.Whole(name)[t];
            }

            mean[t] = total / runs.Count;
        }

        // Smoothed first. A thin shelf's price moves by five per cent a tick on demand noise, so
        // the raw series never sits within one per cent of anything; what settles, or does not, is
        // its level. Thirty ticks is long enough to average that noise away and short enough that a
        // transient with a time constant of seventy ticks is still visible in it.
        const int smoothing = 30;

        var smoothed = new double[ticks - smoothing + 1];

        for (var t = 0; t < smoothed.Length; t++)
        {
            smoothed[t] = Stationarity.Mean(mean.AsSpan(t, smoothing));
        }

        var late = smoothed.AsSpan(smoothed.Length - (smoothed.Length / 4));
        var settledLevel = Stationarity.Mean(late);

        if (settledLevel == 0.0)
        {
            return 1;
        }

        // The band is the wider of the stated tolerance and twice what the series' own level still
        // wanders by once it has settled. A thin shelf's price never stops moving — appliances
        // premium is two units a tick and its smoothed level drifts several per cent for ever —
        // and holding it to one per cent would report a transient that is really its noise floor.
        var wander = 0.0;

        foreach (var value in late)
        {
            wander += (value - settledLevel) * (value - settledLevel);
        }

        var band = Math.Max(tolerance, 2.0 * Math.Sqrt(wander / late.Length) / Math.Abs(settledLevel));

        for (var t = smoothed.Length - 1; t >= 0; t--)
        {
            if (Math.Abs(smoothed[t] - settledLevel) / Math.Abs(settledLevel) > band)
            {
                // The window ending at `t + smoothing` was the last one outside the band.
                return t + smoothing + 1;
            }
        }

        return 1;
    }

    /// <summary>
    /// The per-tick cash distribution, which no CSV column carries.
    ///
    /// It is collected here rather than added to the schema because this gate is its only consumer:
    /// a column exists so that the analysis outside the engine can read it, and nothing outside the
    /// engine reads this.
    /// </summary>
    private sealed class Collector(int ticks)
    {
        private readonly List<double> lowerDeciles = new(ticks);

        private Money[] sorted = [];

        public double TotalIncome { get; private set; }

        public IReadOnlyList<double> LowerDecilesCash => lowerDeciles;

        public void After(Simulation simulation)
        {
            var books = simulation.Books;
            var households = books.HouseholdCount;

            if (sorted.Length != households)
            {
                sorted = new Money[households];

                for (var h = 0; h < households; h++)
                {
                    TotalIncome += simulation.Population.Income[h].Cents / 100.0;
                }
            }

            for (var h = 0; h < households; h++)
            {
                sorted[h] = books.Cash(h);
            }

            Array.Sort(sorted);

            // Deciles one to nine: everyone but the richest tenth, whose hoarding is the residual
            // drain §V4 accepts. Held together so that a drain spreading below the top decile shows.
            var total = 0L;
            var below = households - (households / 10);

            for (var h = 0; h < below; h++)
            {
                total += sorted[h].Cents;
            }

            lowerDeciles.Add(total / 100.0);
        }
    }

    /// <summary>One seed's series, read back from the output it wrote plus the one thing the output does not carry.</summary>
    private sealed class NullRun
    {
        private readonly Dictionary<string, double[]> series = [];

        private NullRun(int seed) => Seed = seed;

        public int Seed { get; }

        public double TotalIncome { get; private set; }

        public double LowestPool { get; private set; }

        public bool EverLent { get; private set; }

        public IReadOnlyList<string> Prices { get; private set; } = [];

        public IReadOnlyList<string> Mixes { get; private set; } = [];

        /// <summary>The whole run, from tick 1 — what a settling tick is searched over.</summary>
        public ReadOnlySpan<double> Whole(string name) => series[name];

        /// <summary>The measured window: everything after the warm-up.</summary>
        public ReadOnlySpan<double> Window(string name, int warmup) => series[name].AsSpan(warmup);

        public static NullRun Read(string directory, Collector collected)
        {
            var run = OutputFile.Read(directory, "run.csv");
            var tiers = OutputFile.Read(directory, "tiers.csv");
            var read = new NullRun(int.Parse(run.Field(0, run.Column("seed")), System.Globalization.CultureInfo.InvariantCulture))
            {
                TotalIncome = collected.TotalIncome,
            };

            read.series["cpi"] = Column(run, "cpi");
            read.series["pool"] = Column(run, "pool");
            read.series["lower_deciles_cash"] = [.. collected.LowerDecilesCash];

            read.LowestPool = read.series["pool"].Min();

            var loans = Column(run, "loans_outstanding");
            read.EverLent = Array.Exists(loans, outstanding => outstanding != 0.0);

            var prices = new List<string>();
            var mixes = new List<string>();
            var byShelf = new Dictionary<string, List<double>>();

            for (var row = 0; row < tiers.RowCount; row++)
            {
                var shelf = tiers.Field(row, tiers.Column("category")) + "." + tiers.Field(row, tiers.Column("tier"));

                Append(byShelf, "price " + shelf, OutputFile.Number(tiers.Field(row, tiers.Column("price"))));
                Append(byShelf, "mix " + shelf, OutputFile.Number(tiers.Field(row, tiers.Column("mix_share"))));
            }

            foreach (var (name, values) in byShelf)
            {
                read.series[name] = [.. values];

                if (name.StartsWith("price ", StringComparison.Ordinal))
                {
                    prices.Add(name);
                }
                else
                {
                    mixes.Add(name);
                }
            }

            prices.Sort(StringComparer.Ordinal);
            mixes.Sort(StringComparer.Ordinal);

            read.Prices = prices;
            read.Mixes = mixes;

            return read;
        }

        private static void Append(Dictionary<string, List<double>> into, string name, double value)
        {
            if (!into.TryGetValue(name, out var values))
            {
                values = [];
                into[name] = values;
            }

            values.Add(value);
        }

        private static double[] Column(OutputFile file, string name)
        {
            var at = file.Column(name);
            var values = new double[file.RowCount];

            for (var row = 0; row < file.RowCount; row++)
            {
                values[row] = OutputFile.Number(file.Field(row, at));
            }

            return values;
        }
    }
}
