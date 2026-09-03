using System.Globalization;
using System.Text;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Engine.Output;

/// <summary>
/// The engine's only output surface: two CSV files, the effective configuration beside them, and a
/// completion marker.
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
    private readonly StringBuilder line = new(512);
    private readonly GoodsTable goods;
    private readonly string directory;
    private readonly string scenario;
    private readonly int runSeed;
    private readonly int tierColumns;
    private readonly int runColumns;

    private int fields;
    private int ticksWritten;
    private bool finished;

    private MetricsWriter(GoodsTable goods, string directory, string scenario, int runSeed, StreamWriter tiers, StreamWriter run)
    {
        this.goods = goods;
        this.directory = directory;
        this.scenario = scenario;
        this.runSeed = runSeed;
        this.tiers = tiers;
        this.run = run;

        tierColumns = WriteHeader(tiers, TierHeader);
        runColumns = WriteHeader(run, RunHeader);
    }

    /// <summary>Rows written to `tiers.csv`, excluding the header.</summary>
    public int TierRows { get; private set; }

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
                Open(directory, "tiers.csv"),
                Open(directory, "run.csv")));
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

            var marker = new StringBuilder();
            marker.AppendLine(CultureInfo.InvariantCulture, $"scenario = \"{scenario}\"");
            marker.AppendLine(CultureInfo.InvariantCulture, $"seed = {runSeed}");
            marker.AppendLine(CultureInfo.InvariantCulture, $"ticks = {ticksWritten}");
            marker.AppendLine(CultureInfo.InvariantCulture, $"tier_rows = {TierRows}");

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
    }

    // ---- the schema ---------------------------------------------------------------------------

    /// <summary>
    /// Columns are **added, never renamed, reordered by meaning, or repurposed**. A reader keyed by
    /// name then survives every later version, and a fixture from an older version still parses —
    /// which is what lets one campaign's files be read beside the next one's.
    /// </summary>
    private static void TierHeader(Action<string> column)
    {
        Keys(column);
        column("category");
        column("tier");
        column("price");
        column("units");
        column("sold");
        column("blocked");
        column("unaffordable");
        column("mix_share");
    }

    private void RunHeader(Action<string> column)
    {
        Keys(column);
        column("cpi");

        foreach (var category in goods.Categories)
        {
            column("cpi_" + category.Name);
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
                Field(goods.Categories[c].Name);
                Field(goods.Tiers[t].Name);
                Field(record.PriceTraded(c, t));
                Field(goods.Units(c, t));
                Field(simulation.Market.Sold(c, t));
                Field(simulation.Market.Blocked(c, t));
                Field(simulation.Market.Unaffordable(c, t));
                Field(PriceIndex.MixShare(simulation.Market, goods, c, t));
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
