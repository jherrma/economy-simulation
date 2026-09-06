using System.Globalization;
using System.Text;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Engine.Output;

/// <summary>
/// The engine's only output surface: three CSV files, the effective configuration beside them, and
/// a completion marker.
///
/// **CSV and nothing else.** No index the analysis might want revised, no percentile, no plot. Every
/// analytical choice made in here is a choice that can only be changed by re-running the campaign,
/// and the campaign is the expensive part. The one derived column is the tier mix share, which
/// 07-02 asks for by name and which is exactly `sold / Σ_t sold` from the same row's neighbours.
///
/// **The marker is what protects the campaign.** E9 launches hundreds of processes and collects
/// their files; a halted run leaves a perfectly plausible partial CSV, and a collector that treats
/// the presence of a file as success will average it into a result. `run.done` is written only
/// after a clean finish, and the collector requires it.
///
/// **Warm-up is written and flagged, never discarded.** V4's claim is that the transient decayed,
/// and that can only be checked against the ticks it decayed over.
///
/// **`cohorts.csv` is long, and `run.csv` did not change to make room for it.** The archetype cut
/// (E10) is two-dimensional — (abstainer, archetype) — and folding it into `run.csv` as columns
/// would be four hundred columns and would break V5a, whose whole claim is that `run.csv` is
/// byte-identical under the identity table. So the cells go into a file of their own, one row per
/// tick per cell, and `run.csv` keeps carrying the cohort totals it always carried. A test asserts
/// the long file sums to them.
///
/// **Every series is written per good and per category** (E11). A row of the goods table is a good;
/// what it rolls up into is its label, and under §3.1 the two coincide, so `cpi_food` and
/// `cpi_category_food` carry the same number and always will for that calibration. The duplication
/// is deliberate: a schema that emitted the roll-up only where it differed from a good would be a
/// schema a reader has to inspect the goods table to parse, and the test that the roll-up is right
/// would have nothing to assert on the one table where it cannot be wrong.
///
/// **No share is written anywhere but the tier mix.** Counts only, per category and per tier, so a
/// share computed from this file is necessarily computed *within* a category. Pooling a share
/// across categories reverses its sign when exclusion moves units out of the denominator
/// (`01-SIMULATION.md` §10.4), and the archetypes were designed to differ in exactly the category
/// weights a pooled figure averages over.
/// </summary>
public sealed class MetricsWriter : IDisposable
{
    /// <summary>Six decimals on every dimensionless number: enough to see a price index move, and fixed, so output is byte-identical across machines.</summary>
    private const string RatioFormat = "0.000000";

    private const int BufferBytes = 1 << 16;

    /// <summary>Cached: `Enum.GetValues` allocates, and this is walked once per row.</summary>
    private static readonly Cohort[] AllCohorts = Enum.GetValues<Cohort>();

    private readonly StreamWriter tiers;
    private readonly StreamWriter run;
    private readonly StreamWriter cohorts;
    private readonly StringBuilder line = new(512);
    private readonly GoodsTable goods;
    private readonly string directory;
    private readonly string scenario;
    private readonly int runSeed;
    private readonly (string Good, double Relative) residue;
    private readonly int tierColumns;
    private readonly int runColumns;
    private readonly int cohortColumns;

    private int fields;
    private int ticksWritten;
    private bool finished;

    private MetricsWriter(
        GoodsTable goods,
        string directory,
        string scenario,
        int runSeed,
        (string Good, double Relative) residue,
        StreamWriter tiers,
        StreamWriter run,
        StreamWriter cohorts)
    {
        this.goods = goods;
        this.directory = directory;
        this.scenario = scenario;
        this.runSeed = runSeed;
        this.residue = residue;
        this.tiers = tiers;
        this.run = run;
        this.cohorts = cohorts;

        tierColumns = WriteHeader(tiers, TierHeader);
        runColumns = WriteHeader(run, RunHeader);
        cohortColumns = WriteHeader(cohorts, CohortHeader);
    }

    /// <summary>Rows written to `tiers.csv`, excluding the header.</summary>
    public int TierRows { get; private set; }

    /// <summary>Rows written to `cohorts.csv`, excluding the header.</summary>
    public int CohortRows { get; private set; }

    /// <summary>Ticks written to `run.csv`.</summary>
    public int Ticks => ticksWritten;

    /// <summary>
    /// Opens the output directory for a run: the two files, and the effective configuration beside
    /// them.
    ///
    /// It takes the simulation rather than a configuration and a seed, so that the scenario, the
    /// seed and the goods table written into the files are the ones the run is actually using.
    /// Passed separately, a seed can disagree with the run it labels, and nothing downstream would
    /// ever notice.
    ///
    /// The configuration is written first and unconditionally, so that a run which halts still
    /// leaves behind what it was trying to do.
    /// </summary>
    public static Result<MetricsWriter> Create(Simulation simulation, string directory)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var parameters = simulation.Parameters;

        try
        {
            Directory.CreateDirectory(directory);

            var configuration = ConfigurationLoader.WriteEffectiveConfiguration(parameters, directory);

            if (!Results.IsOk(configuration))
            {
                return configuration.ToResult<MetricsWriter>();
            }

            var marker = Path.Combine(directory, MarkerFile);

            if (File.Exists(marker))
            {
                File.Delete(marker);
            }

            return Result.Ok(new MetricsWriter(
                simulation.Goods,
                directory,
                parameters.Run.Scenario,
                simulation.RunSeed,
                simulation.ReplacementResidue,
                Open(directory, "tiers.csv"),
                Open(directory, "run.csv"),
                Open(directory, "cohorts.csv")));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<MetricsWriter>($"output: expected to write into {directory}, got {failure.Message}");
        }
    }

    /// <summary>The file whose presence means the run finished cleanly.</summary>
    public static string MarkerFile => "run.done";

    /// <summary>One tick: eighteen rows in `tiers.csv`, one in `run.csv`.</summary>
    public Result Write(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        if (finished)
        {
            return Result.Fail("output: the writer was finished; a run writes its ticks before its marker");
        }

        var record = simulation.Recorded;

        try
        {
            WriteTierRows(simulation, record);
            WriteRunRow(simulation, record);
            WriteCohortRows(simulation, record);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"output, tick {record.Tick}: {failure.Message}");
        }

        ticksWritten++;
        return Results.Ok;
    }

    /// <summary>
    /// Flushes both files and writes the marker. Called only when the run completed; a halted run
    /// disposes without finishing, and leaves output no collector will accept.
    /// </summary>
    public Result Finish()
    {
        if (finished)
        {
            return Results.Ok;
        }

        try
        {
            tiers.Flush();
            run.Flush();
            cohorts.Flush();

            var marker = new StringBuilder();
            marker.AppendLine(CultureInfo.InvariantCulture, $"scenario = \"{scenario}\"");
            marker.AppendLine(CultureInfo.InvariantCulture, $"seed = {runSeed}");
            marker.AppendLine(CultureInfo.InvariantCulture, $"ticks = {ticksWritten}");
            marker.AppendLine(CultureInfo.InvariantCulture, $"tier_rows = {TierRows}");
            marker.AppendLine(CultureInfo.InvariantCulture, $"cohort_rows = {CohortRows}");

            // §3.7's residue, stated once. It is a property of the population this seed drew, not
            // of the files, and it belongs beside them rather than in a per-tick column that would
            // repeat one number three hundred and sixty times.
            marker.AppendLine(CultureInfo.InvariantCulture, $"replacement_residue_good = \"{residue.Good}\"");
            marker.AppendLine(
                CultureInfo.InvariantCulture,
                $"replacement_residue = {residue.Relative.ToString(RatioFormat, CultureInfo.InvariantCulture)}");

            File.WriteAllText(Path.Combine(directory, MarkerFile), marker.ToString());
            finished = true;

            return Results.Ok;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return Result.Fail($"output: expected to finish {directory}, got {failure.Message}");
        }
    }

    public void Dispose()
    {
        tiers.Dispose();
        run.Dispose();
        cohorts.Dispose();
    }

    // ---- the schema ---------------------------------------------------------------------------

    /// <summary>
    /// Columns are **added, never renamed, reordered by meaning, or repurposed**. A reader keyed by
    /// name then survives every later version, and a fixture from an older version still parses —
    /// which is what lets one campaign's files be read beside the next one's. Added includes
    /// *inserted*: `good` sits next to `category` rather than at the end of the row, because a
    /// name-keyed reader does not care and a person reading the file does.
    ///
    /// `category` was not repurposed when E11 split it. It always held the label; under §3.1 the
    /// label was also the row's name, and `good` is the column that now says which row.
    /// </summary>
    private static void TierHeader(Action<string> column)
    {
        Keys(column);
        column("category");
        column("good");
        column("tier");
        column("price");
        column("units");
        column("sold");
        column("blocked");
        column("unaffordable");
        column("mix_share");
        column("category_mix_share");
    }

    private void RunHeader(Action<string> column)
    {
        Keys(column);
        column("cpi");

        foreach (var category in goods.Categories)
        {
            column("cpi_" + category.Name);
        }

        foreach (var label in goods.Labels)
        {
            column("cpi_category_" + label);
        }

        column("money_stock");
        column("loans_outstanding");
        column("loans_live");
        column("pool");
        column("money_created");
        column("money_destroyed");
        column("rationed");

        foreach (var cohort in AllCohorts)
        {
            var name = Name(cohort);

            column(name + "_households");
            column(name + "_cash");
            column(name + "_loans_outstanding");
            column(name + "_debt_service");
            column(name + "_spend");
            column(name + "_quality");
            column(name + "_wanted");
            column(name + "_obtained");
            column(name + "_wait_median");
            column(name + "_wait_median_met");

            foreach (var category in goods.Categories)
            {
                column($"{name}_{category.Name}_wanted");
                column($"{name}_{category.Name}_obtained");
                column($"{name}_{category.Name}_spend");

                foreach (var tier in goods.Tiers)
                {
                    column($"{name}_{category.Name}_{tier.Name}_units");
                }
            }
        }
    }

    /// <summary>
    /// One row per tick per (cohort, archetype) cell — 07-03's measures, cut both ways.
    ///
    /// Long rather than wide, and the archetype named on **every row**: a reader keyed by name then
    /// needs no knowledge of how many types there were, and adding a type adds rows rather than
    /// columns. Under the identity table there is one type called `average` and the file is
    /// `run.csv`'s cohort block again, which is exactly what makes it checkable.
    /// </summary>
    private void CohortHeader(Action<string> column)
    {
        Keys(column);
        column("cohort");
        column("archetype");
        column("households");
        column("cash");
        column("loans_outstanding");
        column("debt_service");
        column("spend");
        column("quality");
        column("wanted");
        column("obtained");
        column("wait_median");
        column("wait_median_met");

        foreach (var category in goods.Categories)
        {
            column($"{category.Name}_wanted");
            column($"{category.Name}_obtained");
            column($"{category.Name}_spend");

            foreach (var tier in goods.Tiers)
            {
                column($"{category.Name}_{tier.Name}_units");
            }
        }
    }

    /// <summary>The cohort's name in a column. Lower case, and stable: renaming one breaks every reader.</summary>
    private static string Name(Cohort cohort) => cohort == Cohort.Abstainer ? "abstainer" : "borrower";

    private static void Keys(Action<string> column)
    {
        column("scenario");
        column("seed");
        column("tick");
        column("warmup");
    }

    private void WriteTierRows(Simulation simulation, TickRecord record)
    {
        for (var c = 0; c < goods.CategoryCount; c++)
        {
            for (var t = 0; t < goods.TierCount; t++)
            {
                Begin(record);
                Field(goods.Labels[goods.LabelOf(c)]);
                Field(goods.Categories[c].Name);
                Field(goods.Tiers[t].Name);
                Field(record.PriceTraded(c, t));
                Field(goods.Units(c, t));
                Field(simulation.Market.Sold(c, t));
                Field(simulation.Market.Blocked(c, t));
                Field(simulation.Market.Unaffordable(c, t));
                Field(PriceIndex.MixShare(simulation.Market, goods, c, t));
                Field(PriceIndex.LabelMixShare(simulation.Market, goods, c, t));
                End(tiers, tierColumns, "tiers.csv");

                TierRows++;
            }
        }
    }

    private void WriteRunRow(Simulation simulation, TickRecord record)
    {
        Begin(record);
        Field(record.Cpi);

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            Field(record.CategoryIndex(c));
        }

        for (var l = 0; l < goods.Labels.Count; l++)
        {
            Field(record.LabelIndex(l));
        }

        Field(record.MoneyStock);
        Field(record.LoansOutstanding);
        Field(record.LoansLive);
        Field(record.Pool);
        Field(record.MoneyCreated);
        Field(record.MoneyDestroyed);
        Field(record.Rationed);

        var cohorts = simulation.Cohorts;

        foreach (var cohort in AllCohorts)
        {
            Field(cohorts.Households(cohort));
            Field(cohorts.Cash(cohort));
            Field(cohorts.LoansOutstanding(cohort));
            Field(cohorts.DebtService(cohort));
            Field(cohorts.Spend(cohort));
            Field(cohorts.Quality(cohort));
            Field(cohorts.Wanted(cohort));
            Field(cohorts.Obtained(cohort));
            Field(cohorts.WaitMedian(cohort));
            Field(cohorts.WaitMedianMet(cohort));

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                Field(cohorts.Wanted(cohort, c));
                Field(cohorts.Obtained(cohort, c));
                Field(cohorts.Spend(cohort, c));

                for (var t = 0; t < goods.TierCount; t++)
                {
                    Field(cohorts.Units(cohort, c, t));
                }
            }
        }

        End(run, runColumns, "run.csv");
    }

    private void WriteCohortRows(Simulation simulation, TickRecord record)
    {
        var metrics = simulation.Cohorts;

        foreach (var cell in metrics.Cells)
        {
            Begin(record);
            Field(Name(cell.Cohort));
            Field(metrics.ArchetypeNames[cell.Archetype]);
            Field(metrics.Households(cell));
            Field(metrics.Cash(cell));
            Field(metrics.LoansOutstanding(cell));
            Field(metrics.DebtService(cell));
            Field(metrics.Spend(cell));
            Field(metrics.Quality(cell));
            Field(metrics.Wanted(cell));
            Field(metrics.Obtained(cell));
            Field(metrics.WaitMedian(cell));
            Field(metrics.WaitMedianMet(cell));

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                Field(metrics.Wanted(cell, c));
                Field(metrics.Obtained(cell, c));
                Field(metrics.Spend(cell, c));

                for (var t = 0; t < goods.TierCount; t++)
                {
                    Field(metrics.Units(cell, c, t));
                }
            }

            End(cohorts, cohortColumns, "cohorts.csv");

            CohortRows++;
        }
    }

    // ---- rows ---------------------------------------------------------------------------------

    private static StreamWriter Open(string directory, string name)
    {
        var stream = new FileStream(Path.Combine(directory, name), FileMode.Create, FileAccess.Write, FileShare.Read, BufferBytes);

        // Buffered on purpose, and not flushed per row: a run writes about seven thousand rows,
        // and a flush per row is a syscall per row inside the loop being measured.
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), BufferBytes)
        {
            AutoFlush = false,
            NewLine = "\n",
        };
    }

    private static int WriteHeader(StreamWriter into, Action<Action<string>> header)
    {
        var columns = new List<string>();
        header(columns.Add);

        into.WriteLine(string.Join(',', columns));

        return columns.Count;
    }

    private void Begin(TickRecord record)
    {
        line.Clear();
        fields = 0;

        Field(scenario);
        Field(runSeed);
        Field(record.Tick);
        Field(record.IsWarmup ? 1 : 0);
    }

    private void End(StreamWriter into, int expected, string file)
    {
        if (fields != expected)
        {
            throw new InvalidOperationException(
                $"{file}: the header names {expected} columns and the row wrote {fields}. "
                + "Header and row are written by two methods and must be changed together.");
        }

        into.WriteLine(line);
    }

    private void Field(string value) => Separate().Append(value);

    private void Field(int value) => Separate().Append(CultureInfo.InvariantCulture, $"{value}");

    private void Field(Money value) => Separate().Append(value.ToCsv());

    private void Field(double value) =>
        Separate().Append(value.ToString(RatioFormat, CultureInfo.InvariantCulture));

    private StringBuilder Separate()
    {
        if (fields++ > 0)
        {
            line.Append(',');
        }

        return line;
    }
}
