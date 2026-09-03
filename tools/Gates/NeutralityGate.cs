using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>How far one series' measured-window mean strayed from the baseline's, and how well it is measured.</summary>
/// <param name="Excursion">Signed, relative to the baseline's own window mean.</param>
/// <param name="RelativeStandardError">The spread of that mean across seeds, relative to itself. What the series can resolve.</param>
public sealed record Deviation(WindowMeans.Series Series, double Excursion, double RelativeStandardError, double Was, double Became)
{
    public double Size => Math.Abs(Excursion);

    public override string ToString() =>
        Invariant($"{Series}: window mean {Was:G8} became {Became:G8} ({Size:G3} relative; the series resolves {RelativeStandardError:G3})");
}

/// <summary>What holding one campaign against its baseline found, one seed at a time.</summary>
/// <param name="Structural">A label, or the shape of a file, that differs. Never rounding, always a failure.</param>
/// <param name="FirstDivergentTick">The first tick at which any real cell differs at all. Zero when none does.</param>
/// <param name="Tipped">Real cells that differ, over the whole run.</param>
/// <param name="RealCells">How many were compared, so <paramref name="Tipped"/> has a denominator.</param>
public sealed record NeutralityComparison(Difference? Structural, int FirstDivergentTick, int Tipped, int RealCells);

/// <summary>
/// **V3** — multiply every nominal quantity by `c` and nothing real may move.
///
/// What scales: `M0`, all cash, the pool, every opening tier price, every income, `price_floor`,
/// `a_g`, and every outstanding principal. All but three of those are *derived*, so the gate sets
/// three parameters — `mean_income`, each category's `price_ref`, and `price_floor` — and the rest
/// follow. `a_g` follows too, which is the sharpest trap here: `a_g = necessity · v · mean_income`
/// is a floor in euros per tick and must scale, while `b_g`, the coefficient on income, is a pure
/// number and must not. Scaling `b_g` as well produces a *near*-neutral result, which is worse than
/// an obviously broken one because it reads as noise.
///
/// What must not scale: `loan_rate`, `λ`, `b_g`, `v_g`, `necessity_g`, `k`, `σ`, every share and
/// every multiplier. They are pure numbers.
///
/// This gate exists because of the defect that took longest to find in the draft model: a value in
/// utility units divided by a cost in euros pinned the real price level to the utility scale, so
/// doubling every price halved every score while the threshold stood still and demand collapsed for
/// no economic reason. The current design is homogeneous of degree zero by construction — `Δvalue`
/// scales with income, `Δcost` with price.
///
/// **What it compares, and why it is not a byte comparison.** Money is integer cents, and rounding
/// to the cent does not commute with scaling: `round(c·x) ≠ c·round(x)` for about half of all `x`,
/// at `c = 2` as much as at `c = 0.5`. So the two runs' incomes differ by up to half a cent each —
/// and this model amplifies that. A household spends down to nearly nothing every tick, so whether
/// the last increment it reaches for costs one cent more than it holds is a genuine knife edge; one
/// household landing on budget instead of standard changes who gets the last unit of a rationed
/// shelf, and the two trajectories decorrelate within a few dozen ticks
/// (`01-SIMULATION.md` §10.2). Requiring the two runs to be identical tick by tick would be
/// requiring the model not to be what it is.
///
/// What neutrality does say is that the **equilibrium** is unchanged, and that is what is compared:
/// the mean of every series over the measured window, paired by seed. The gate runs a control
/// beside it — one cent added to `mean_income`, with no scaling at all, the smallest real change
/// this model can express — so that the tolerance can be read against the model's own resolution
/// instead of taken on trust. See `spec/03-VERIFICATION.md` §V3.
/// </summary>
public static class NeutralityGate
{
    /// <summary>The two scale factors §V3 names. Both exact on the default parameters: no cent is lost setting the run up.</summary>
    public static IReadOnlyList<double> Scales { get; } = [2.0, 0.5];

    /// <summary>
    /// The floor under how far a measured-window mean may move, relative to itself, whatever the
    /// control says. Two orders of magnitude below the failure this gate exists for: the draft's
    /// defect halved every score and collapsed demand by tens of percent.
    /// </summary>
    public const double Tolerance = 2e-3;

    /// <summary>
    /// How much further than the control a scaled run may move a series.
    ///
    /// The control is one cent on `mean_income` with nothing scaled — the smallest *real* change
    /// this economy can express — and it moves the well-measured series by two or three parts in a
    /// thousand, because the model amplifies any difference at all into a different trajectory
    /// (`01-SIMULATION.md` §10.2). Re-denominating the unit of account must not disturb the economy
    /// by more than the same order as that. Twice, rather than once, because both numbers are
    /// themselves measured on a finite seed set.
    /// </summary>
    public const double ControlFactor = 2.0;

    /// <summary>
    /// How finely a series must be measured before it is held to anything.
    ///
    /// A series' window mean varies from seed to seed, and the spread of that variation is what the
    /// series can resolve. Asking one to agree more closely than it resolves is asking the model to
    /// be less noisy than it is: a shelf that sells one unit a fortnight resolves nothing, and the
    /// median wait resolves a quarter of itself (`spec/03-VERIFICATION.md` §V4 exempts it for the
    /// same reason). Both are still measured and reported; neither is required to agree.
    /// </summary>
    public const double Resolution = 1e-3;

    /// <summary>
    /// The three parameters that carry a unit of account. Everything else nominal in this model is
    /// derived from one of them: opening cash and the pool from `mean_income`, the eighteen opening
    /// prices from `price_ref`, `a_g` from `mean_income`, and every loan principal from a price.
    ///
    /// `scalePriceFloor` is false only for the test that proves this gate can fail. A floor left in
    /// old money is the likeliest first failure of V3 in any implementation, because it is the one
    /// nominal parameter that does not look like a price: it is a guard against dividing by zero,
    /// it is written once, and at the default of one euro against prices of one to sixteen hundred
    /// it never binds — so leaving it unscaled is invisible until a scenario prices a shelf near it.
    /// </summary>
    public static SimulationParameters Scale(SimulationParameters parameters, double scale, bool scalePriceFloor = true)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        return parameters with
        {
            Income = parameters.Income with { MeanIncome = parameters.Income.MeanIncome.Scaled(scale) },
            Categories = [.. parameters.Categories.Select(c => c with { PriceRef = c.PriceRef.Scaled(scale) })],
            Prices = parameters.Prices with
            {
                PriceFloor = scalePriceFloor ? parameters.Prices.PriceFloor.Scaled(scale) : parameters.Prices.PriceFloor,
            },
        };
    }

    /// <summary>
    /// The control: one cent on `mean_income`, nothing scaled. The smallest change to this economy
    /// that is real rather than arithmetic, and therefore the yardstick the tolerance is read
    /// against. It is never required to pass — it is a different economy — only reported.
    /// </summary>
    public static SimulationParameters OneCent(SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters with { Income = parameters.Income with { MeanIncome = parameters.Income.MeanIncome + new Money(1) } };
    }

    public static GateReport Run(
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        Workspace workspace,
        bool scalePriceFloor = true)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(workspace);

        var report = new GateReport("V3 — nominal neutrality");

        Scaling(report.Check("the scale factors are exact on these parameters"), parameters, scalePriceFloor);
        PureNumbers(report.Check("pure numbers are left alone"), parameters, scalePriceFloor);

        foreach (var scenario in new[] { Scenarios.CreditOff(parameters), Scenarios.CreditHigh(parameters) })
        {
            var name = scenario.Run.Scenario;
            var baseline = workspace.Arm(Invariant($"{name}-c1"));
            var ran = Runs.Execute(scenario, seeds, baseline);

            if (ran.IsFailed)
            {
                Refused(report.Check(Invariant($"{name} at c = 1 completes")), ran.Errors);
                continue;
            }

            // The yardstick first: what a change of one cent — real, not arithmetic — does to these
            // same window means. Reported, never required, and the bar the scaled runs are held to.
            var control = Arm(
                report.Check(Invariant($"{name} control: one cent on mean_income, nothing scaled")),
                OneCent(scenario),
                seeds,
                baseline,
                workspace.Arm(Invariant($"{name}-control")),
                scale: 1.0,
                against: null);

            foreach (var scale in Scales)
            {
                Arm(
                    report.Check(Invariant($"{name}, everything nominal x {scale}")),
                    Scale(scenario, scale, scalePriceFloor),
                    seeds,
                    baseline,
                    workspace.Arm(Invariant($"{name}-c{scale}")),
                    scale,
                    against: control);
            }
        }

        return report;
    }

    private static IReadOnlyDictionary<WindowMeans.Series, Deviation> Arm(
        GateCheck check,
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        string baseline,
        string arm,
        double scale,
        IReadOnlyDictionary<WindowMeans.Series, Deviation>? against)
    {
        var ran = Runs.Execute(parameters, seeds, arm);

        if (ran.IsFailed)
        {
            Refused(check, ran.Errors);

            return new Dictionary<WindowMeans.Series, Deviation>();
        }

        if (!Census(check, seeds, baseline, arm))
        {
            return new Dictionary<WindowMeans.Series, Deviation>();
        }

        var deviations = Windows(check, seeds, baseline, arm, scale);

        if (against is not null)
        {
            Judge(check, deviations, against);
        }

        return deviations;
    }

    /// <summary>
    /// The criterion, series by series: a scaled run may not move a series further than the control
    /// does, by more than <see cref="ControlFactor"/> — and never further than
    /// <see cref="Tolerance"/> whatever the control says, so that a control which has itself gone
    /// wrong cannot license anything.
    /// </summary>
    private static void Judge(
        GateCheck check,
        IReadOnlyDictionary<WindowMeans.Series, Deviation> arm,
        IReadOnlyDictionary<WindowMeans.Series, Deviation> control)
    {
        Deviation? tightest = null;
        var worstRatio = 0.0;
        var held = 0;

        foreach (var (series, deviation) in arm)
        {
            if (deviation.RelativeStandardError > Resolution)
            {
                continue;
            }

            held++;

            var allowed = Math.Max(Tolerance, ControlFactor * (control.TryGetValue(series, out var yardstick) ? yardstick.Size : 0.0));
            var ratio = deviation.Size / allowed;

            if (ratio > worstRatio)
            {
                worstRatio = ratio;
                tightest = deviation;
            }

            if (deviation.Size > allowed)
            {
                check.Fail(Invariant(
                    $"{deviation} — more than the {allowed:G3} the control allows here. The equilibrium moved, which is what nominal neutrality forbids."));
            }
        }

        check.Observe(Invariant($"{held} series resolve better than {Resolution:G3} and were held to the control"));

        if (tightest is not null)
        {
            check.Observe(Invariant($"closest to its bound: {tightest} — {worstRatio:P0} of what was allowed"));
        }
    }

    /// <summary>
    /// The row-by-row pass: labels and file shapes must match exactly, and everything real that
    /// differs is counted rather than reported one by one.
    ///
    /// The count is an observation, never a criterion. Half the real cells of a 360-tick run differ
    /// between two arms of this gate, and that is the model rather than a fault in it: cent rounding
    /// tips an occasional knife-edge decision, and one household landing on budget instead of
    /// standard changes who gets the last unit of a rationed shelf (`01-SIMULATION.md` §10.2).
    /// What is worth knowing is where the two runs stopped agreeing, because money illusion would
    /// have them disagree from tick 1.
    /// </summary>
    private static bool Census(GateCheck check, IReadOnlyList<int> seeds, string baseline, string arm)
    {
        var firstTick = 0;
        var tipped = 0;
        var realCells = 0;

        foreach (var seed in seeds)
        {
            var compared = Compare(Runs.Directory(baseline, seed), Runs.Directory(arm, seed));

            if (compared.Structural is not null)
            {
                check.Fail(Invariant($"seed {seed}: {compared.Structural}"));
                check.Fail("A label, or the shape of a file, is not something rounding can move. These are not the same run.");

                return false;
            }

            if (compared.FirstDivergentTick > 0)
            {
                firstTick = firstTick == 0 ? compared.FirstDivergentTick : Math.Min(firstTick, compared.FirstDivergentTick);
            }

            tipped += compared.Tipped;
            realCells += compared.RealCells;
        }

        var share = realCells == 0 ? 0.0 : (double)tipped / realCells;

        check.Observe(firstTick == 0
            ? Invariant($"identical in every one of {realCells} real cells")
            : Invariant($"identical through tick {firstTick - 1}, then {tipped} of {realCells} real cells differ ({share:P1})"));

        return true;
    }

    /// <summary>
    /// The comparison that decides the gate: every series' mean over the measured window, against
    /// the baseline's, paired by seed.
    ///
    /// **A series is compared only when it is measured well enough to be worth comparing.** Its
    /// window mean varies from seed to seed; the spread of that variation is what the series can
    /// resolve, and asking a series to agree more closely than it resolves is asking the model to
    /// be less noisy than it is. A shelf that sells one unit a fortnight resolves nothing; the CPI
    /// resolves parts in ten thousand. Both are reported. Only the second is required to agree.
    ///
    /// The rule is a rule rather than a list of column names on purpose: the schema is additive, and
    /// a hand-written list would quietly stop testing every column added after it.
    /// </summary>
    private static IReadOnlyDictionary<WindowMeans.Series, Deviation> Windows(
        GateCheck check,
        IReadOnlyList<int> seeds,
        string baseline,
        string arm,
        double scale)
    {
        var before = seeds.Select(seed => WindowMeans.Of(Runs.Directory(baseline, seed))).ToList();
        var after = seeds.Select(seed => WindowMeans.Of(Runs.Directory(arm, seed))).ToList();
        var deviations = new Dictionary<WindowMeans.Series, Deviation>();

        Deviation? resolved = null;
        Deviation? unresolved = null;

        foreach (var name in before[0].Measured)
        {
            if (!after[0].Has(name))
            {
                check.Fail(Invariant($"{name} is in the baseline and not in the other run"));

                continue;
            }

            // Money is brought back to the baseline's units before anything is compared; a count or
            // a pure number is already in them.
            var factor = before[0].Kind(name) == Quantity.Money ? scale : 1.0;
            var baselineMeans = before.ConvertAll(w => w.Mean(name));
            var was = baselineMeans.Average();
            var became = after.ConvertAll(w => w.Mean(name) / factor).Average();

            if (was == 0.0)
            {
                // A series that is identically zero is not noisy, it is absent — no loans with
                // credit off, no money created, no wait among households served the tick they ask.
                // It becoming non-zero at all is real, however small.
                if (became != 0.0)
                {
                    check.Fail(Invariant($"{name} was zero throughout the baseline and averages {became:G6} in the other run"));
                }

                continue;
            }

            var deviation = new Deviation(
                name,
                (became - was) / Math.Abs(was),
                StandardError(baselineMeans) / Math.Abs(was),
                was,
                became);

            deviations[name] = deviation;

            if (deviation.RelativeStandardError > Resolution)
            {
                unresolved = Worse(unresolved, deviation);
            }
            else
            {
                resolved = Worse(resolved, deviation);
            }
        }

        if (resolved is not null)
        {
            check.Observe(Invariant($"worst well-measured series: {resolved}"));
        }

        if (unresolved is not null)
        {
            check.Observe(Invariant($"worst series too noisy to hold: {unresolved}"));
        }

        return deviations;
    }

    private static Deviation? Worse(Deviation? worst, Deviation candidate) =>
        worst is null || candidate.Size > worst.Size ? candidate : worst;

    /// <summary>
    /// The spread of a window mean across seeds. Taken across whole runs rather than across ticks
    /// on purpose: a run's ticks are autocorrelated, and a standard error computed over them would
    /// claim a precision the series does not have.
    /// </summary>
    private static double StandardError(IReadOnlyList<double> perSeed)
    {
        if (perSeed.Count < 2)
        {
            return 0.0;
        }

        var mean = perSeed.Average();
        var sum = 0.0;

        foreach (var value in perSeed)
        {
            sum += (value - mean) * (value - mean);
        }

        return Math.Sqrt(sum / (perSeed.Count - 1)) / Math.Sqrt(perSeed.Count);
    }

    /// <summary>
    /// One seed, row by row: labels exactly, and a census of everything real that differs.
    ///
    /// Money and price indices are not examined here — they are compared as window means, where the
    /// arithmetic of cent rounding does not drown the economics.
    /// </summary>
    public static NeutralityComparison Compare(string baseline, string scaled)
    {
        var firstTick = 0;
        var tipped = 0;
        var realCells = 0;

        foreach (var file in new[] { "run.csv", "tiers.csv" })
        {
            var left = OutputFile.Read(baseline, file);
            var right = OutputFile.Read(scaled, file);

            if (left.RowCount != right.RowCount || left.Columns.Count != right.Columns.Count)
            {
                return new NeutralityComparison(
                    new Difference(
                        file,
                        0,
                        "(the shape of the file)",
                        Invariant($"{left.RowCount} rows x {left.Columns.Count} columns"),
                        Invariant($"{right.RowCount} rows x {right.Columns.Count} columns")),
                    firstTick,
                    tipped,
                    realCells);
            }

            var tick = left.Column("tick");

            for (var column = 0; column < left.Columns.Count; column++)
            {
                var name = left.Columns[column];
                var kind = OutputFile.IsPriceIndex(name) ? Quantity.PriceIndex : OutputFile.KindOf(left.Field(0, column));

                if (kind is Quantity.Money or Quantity.PriceIndex)
                {
                    continue;
                }

                for (var row = 0; row < left.RowCount; row++)
                {
                    var was = left.Field(row, column);
                    var now = right.Field(row, column);
                    var differs = !string.Equals(was, now, StringComparison.Ordinal);

                    if (kind == Quantity.Text)
                    {
                        // A label is structure, not measurement: the scenario, the category, the
                        // tier. Nothing rounds it and nothing may move it.
                        if (differs)
                        {
                            return new NeutralityComparison(
                                new Difference(file, OutputFile.Line(row), name, was, now),
                                firstTick,
                                tipped,
                                realCells);
                        }

                        continue;
                    }

                    realCells++;

                    if (differs)
                    {
                        tipped++;
                        var at = int.Parse(left.Field(row, tick), System.Globalization.CultureInfo.InvariantCulture);
                        firstTick = firstTick == 0 ? at : Math.Min(firstTick, at);
                    }
                }
            }
        }

        return new NeutralityComparison(null, firstTick, tipped, realCells);
    }

    /// <summary>
    /// The three parameters the gate sets are exactly `c` times what they were — checked before
    /// anything is run. If `mean_income` or a `price_ref` had itself been rounded away from `c`
    /// times the original, the gate would be measuring its own arithmetic and reporting it as the
    /// model's.
    ///
    /// The quantities the *model* derives from them are reported rather than required. `a_g` is one:
    /// `necessity · v · mean_income` for clothing is 3575 cents, and half of that is not a whole
    /// number of cents. There is nothing to fix there — it is the same half-cent the whole gate is
    /// built to tolerate and the control run is there to calibrate — but it is worth naming, because
    /// a reader who expects `a_g` to scale exactly should be told where it cannot.
    /// </summary>
    private static void Scaling(GateCheck check, SimulationParameters parameters, bool scalePriceFloor)
    {
        var inexact = new List<string>();

        foreach (var scale in Scales)
        {
            var scaled = Scale(parameters, scale, scalePriceFloor);

            Exact(check, scale, "mean_income", parameters.Income.MeanIncome, scaled.Income.MeanIncome);

            for (var c = 0; c < parameters.Categories.Count; c++)
            {
                Exact(
                    check,
                    scale,
                    Invariant($"price_ref[{parameters.Categories[c].Name}]"),
                    parameters.Categories[c].PriceRef,
                    scaled.Categories[c].PriceRef);

                // a_g is a floor in euros per tick and has to scale; it is derived from mean_income,
                // so it does. b_g is a coefficient on income and must not; it is a pure number, so
                // it cannot. Getting these two the wrong way round is the second most likely failure
                // of this gate, and it produces a near-neutral result rather than a broken one.
                var was = parameters.Categories[c].Floor(parameters.Income.MeanIncome);
                var now = scaled.Categories[c].Floor(scaled.Income.MeanIncome);

                if (Math.Abs(now.Cents - (was.Cents * scale)) > 1e-9)
                {
                    inexact.Add(Invariant($"a_g[{parameters.Categories[c].Name}] at c = {scale}: {was.Cents} cents does not divide, so {was.Cents * scale} became {now.Cents}"));
                }
            }

            if (scalePriceFloor)
            {
                Exact(check, scale, "price_floor", parameters.Prices.PriceFloor, scaled.Prices.PriceFloor);
            }
        }

        var floor = parameters.Categories[0].Floor(parameters.Income.MeanIncome).ToCsv();
        var slope = parameters.Categories[0].IncomeSlope;

        check.Observe(Invariant($"a_g[food] = {floor} scales with mean_income; b_g[food] = {slope:0.######} is a pure number and does not"));

        foreach (var line in inexact)
        {
            check.Observe(line + " — half a cent, the same rounding the control calibrates");
        }
    }

    private static void Exact(GateCheck check, double scale, string name, Money was, Money now)
    {
        var expected = was.Cents * scale;

        if (Math.Abs(now.Cents - expected) > 1e-9)
        {
            check.Fail(Invariant(
                $"{name} at c = {scale}: {was.Cents} cents scales to {expected}, which is not a whole number of cents — the run got {now.Cents}. Choose a scale factor these parameters divide by, or the gate measures its own rounding."));
        }
    }

    /// <summary>Everything V3 names as a pure number is untouched by the scaling. One line, and it covers the whole class of error it is aimed at.</summary>
    private static void PureNumbers(GateCheck check, SimulationParameters parameters, bool scalePriceFloor)
    {
        foreach (var scale in Scales)
        {
            var scaled = Scale(parameters, scale, scalePriceFloor);

            if (scaled.Decision != parameters.Decision)
            {
                check.Fail(Invariant($"c = {scale} moved [decision]: λ, σ_w, the subsistence share and the buffer are pure numbers"));
            }

            if (scaled.Credit != parameters.Credit)
            {
                check.Fail(Invariant($"c = {scale} moved [credit]: loan_rate is a percentage and θ is a probability"));
            }

            if (scaled.Prices.K != parameters.Prices.K || scaled.Prices.Rationing != parameters.Prices.Rationing)
            {
                check.Fail(Invariant($"c = {scale} moved k or the rationing rule; both are pure"));
            }

            if (!scaled.Tiers.SequenceEqual(parameters.Tiers))
            {
                check.Fail(Invariant($"c = {scale} moved a tier multiplier or unit share; all six are pure"));
            }

            for (var c = 0; c < parameters.Categories.Count; c++)
            {
                var was = parameters.Categories[c];
                var now = scaled.Categories[c];

                if (was.V != now.V || was.Necessity != now.Necessity || was.Life != now.Life || was.Capacity != now.Capacity)
                {
                    check.Fail(Invariant($"c = {scale} moved something pure in category '{was.Name}': only price_ref carries euros"));
                }
            }
        }
    }

    private static void Refused(GateCheck check, IEnumerable<IError> errors)
    {
        foreach (var error in errors)
        {
            check.Fail(error.Message);
        }
    }
}
