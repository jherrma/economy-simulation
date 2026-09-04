using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Engine.Output;

/// <summary>
/// The two cohorts the whole project exists to compare. Fixed at initialisation, identical across
/// scenarios for a given seed, and exhaustive: every household is in exactly one.
/// </summary>
public enum Cohort
{
    /// <summary>`θ = 0` in every scenario. What happens to these households is the finding.</summary>
    Abstainer,

    /// <summary>Everyone else — able to borrow, whether or not they ever do.</summary>
    Borrower,
}

/// <summary>
/// One cell of the two-dimensional cut: a cohort and an archetype.
///
/// A type rather than a pair of `int`s because the per-cohort and per-cell readings would otherwise
/// have the same signature — `Spend(cohort, 3)` meaning either "category 3" or "archetype 3" is a
/// bug that compiles.
/// </summary>
/// <param name="Cohort">Abstainer or borrower.</param>
/// <param name="Archetype">Index into <see cref="CohortMetrics.ArchetypeNames"/>.</param>
public readonly record struct CohortCell(Cohort Cohort, int Archetype);

/// <summary>
/// Four things about each cohort, ordered by how easy they are to argue with.
///
/// `share_of_wanted_obtained` and `wait` say a cohort is served **less often** or **later**.
/// `tier_mix` and `quality_index` say it is served **worse**, and that is the sharper claim and the
/// one a reader recognises: credit did not stop the abstainer owning a phone, it moved them to a
/// cheaper phone. With supply fixed, total real consumption is capped, so the question this model
/// can answer is never how much the town consumes — only who gets it, and in what quality.
///
/// Everything here is a **raw series**. The differencing between scenarios happens outside the
/// engine, where it can be revised without re-running the campaign.
///
/// **The cut is two-dimensional** since E10: every quantity is accumulated per
/// (cohort, archetype) **cell**, and the per-cohort figure is the sum over the archetypes. That
/// ordering matters — a cohort total computed from cell totals is exact, while a cohort *median*
/// is not, so the wait medians sum their histograms rather than their answers.
///
/// Resist widening the cut further. Four types times two cohorts is eight cells and each cell is a
/// quarter of the households it used to be; the per-cell noise grows accordingly. If a cell gets
/// thin, the answer is more seeds, not a finer cut.
/// </summary>
public sealed class CohortMetrics
{
    private const int CohortCount = 2;

    private readonly GoodsTable goods;
    private readonly int archetypes;
    private readonly int cells;
    private readonly int[] households;

    // Per cell.
    private readonly Money[] cash;
    private readonly Money[] loansOutstanding;
    private readonly Money[] debtService;
    private readonly Money[] spend;
    private readonly double[] quality;
    private readonly double[] waitMedian;
    private readonly double[] waitMedianMet;
    private readonly int[][] waits;
    private readonly int[][] waitsMet;

    // Per cohort, over its archetypes — a median is not a sum, so it is taken from the summed
    // histogram rather than from the cells' answers.
    private readonly double[] cohortWaitMedian = new double[CohortCount];
    private readonly double[] cohortWaitMedianMet = new double[CohortCount];
    private readonly int[][] cohortWaits;
    private readonly int[][] cohortWaitsMet;

    // Per cell, per category.
    private readonly int[] wanted;
    private readonly int[] obtained;
    private readonly Money[] spendByCategory;

    // Per cell, per category, per tier.
    private readonly int[] units;

    public CohortMetrics(GoodsTable goods, Households population, int ticks)
    {
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(population);
        ArgumentOutOfRangeException.ThrowIfLessThan(ticks, 1);

        this.goods = goods;

        archetypes = population.ArchetypeNames.Count;
        ArchetypeNames = population.ArchetypeNames;
        cells = CohortCount * archetypes;

        households = new int[cells];
        cash = new Money[cells];
        loansOutstanding = new Money[cells];
        debtService = new Money[cells];
        spend = new Money[cells];
        quality = new double[cells];
        waitMedian = new double[cells];
        waitMedianMet = new double[cells];

        wanted = new int[cells * goods.CategoryCount];
        obtained = new int[cells * goods.CategoryCount];
        spendByCategory = new Money[cells * goods.CategoryCount];
        units = new int[cells * goods.GoodCount];

        // A histogram rather than a list: the median has to be computed every tick inside a run
        // that allocates nothing, and a wait is a small non-negative integer. The last bucket
        // holds everything at or beyond the run's length, which nothing can exceed.
        waits = Histograms(cells, ticks);
        waitsMet = Histograms(cells, ticks);
        cohortWaits = Histograms(CohortCount, ticks);
        cohortWaitsMet = Histograms(CohortCount, ticks);

        for (var h = 0; h < population.Count; h++)
        {
            households[Cell(population, h)]++;
        }
    }

    /// <summary>The archetype table's types, in the order the loader put them. One column of the cut.</summary>
    public IReadOnlyList<string> ArchetypeNames { get; }

    /// <summary>How many archetypes the cut has. One under the identity table.</summary>
    public int ArchetypeCount => archetypes;

    /// <summary>Which cohort a household is in. By construction, never changes and never depends on the scenario.</summary>
    public static Cohort Of(Households population, int household)
    {
        ArgumentNullException.ThrowIfNull(population);

        return population.IsAbstainer[household] ? Cohort.Abstainer : Cohort.Borrower;
    }

    /// <summary>
    /// Which (cohort, archetype) cell a household is in. Both dimensions are fixed at
    /// initialisation and identical across scenarios for a given seed, which is what makes the
    /// comparison paired in both of them.
    /// </summary>
    public static int Cell(Households population, int household)
    {
        ArgumentNullException.ThrowIfNull(population);

        return ((int)Of(population, household) * population.ArchetypeNames.Count)
               + population.Archetype[household];
    }

    /// <summary>Every cell of the cut, cohort-major — the rows of the cohort file.</summary>
    public IEnumerable<CohortCell> Cells
    {
        get
        {
            foreach (var cohort in new[] { Cohort.Abstainer, Cohort.Borrower })
            {
                for (var a = 0; a < archetypes; a++)
                {
                    yield return new CohortCell(cohort, a);
                }
            }
        }
    }

    public int Households(Cohort cohort) => Sum(households, cohort);

    public int Households(CohortCell cell) => households[Index(cell)];

    public Money Cash(Cohort cohort) => Sum(cash, cohort);

    public Money Cash(CohortCell cell) => cash[Index(cell)];

    public Money LoansOutstanding(Cohort cohort) => Sum(loansOutstanding, cohort);

    public Money LoansOutstanding(CohortCell cell) => loansOutstanding[Index(cell)];

    public Money DebtService(Cohort cohort) => Sum(debtService, cohort);

    public Money DebtService(CohortCell cell) => debtService[Index(cell)];

    /// <summary>What the cohort spent on goods this tick, at the posted prices of the tiers it landed on.</summary>
    public Money Spend(Cohort cohort) => Sum(spend, cohort);

    public Money Spend(CohortCell cell) => spend[Index(cell)];

    /// <summary>
    /// Units obtained this tick weighted by `value_mult`, so a cohort that holds its unit count by
    /// buying worse goods is not recorded as unaffected. Raw, not per household: the cohort sizes
    /// are in the same file.
    /// </summary>
    public double Quality(Cohort cohort) => Sum(quality, cohort);

    public double Quality(CohortCell cell) => quality[Index(cell)];

    /// <summary>
    /// The median wait, in ticks, over every **durable** want the cohort faced this tick — those
    /// met, at the wait they were met after, and those still open, at their current age.
    ///
    /// Counting the unmet ones is not a detail. Under `credit_high` the abstainers most affected
    /// are exactly the ones who never get served, and a median over completed waits alone would
    /// drop them and report an improvement. Food and leisure are excluded because they are wanted
    /// every tick and met or not the same tick, so their zeros would swamp the number that matters.
    /// </summary>
    public double WaitMedian(Cohort cohort) => cohortWaitMedian[(int)cohort];

    public double WaitMedian(CohortCell cell) => waitMedian[Index(cell)];

    /// <summary>
    /// The median wait over the durable wants the cohort **actually had met** this tick.
    ///
    /// It exists because <see cref="WaitMedian"/> cannot be compared across ticks. A stable
    /// population of households at the poor end never gets served at all (`01-SIMULATION.md`
    /// §10.1); their wants stay open and age by one every tick, so the median over all open wants
    /// climbs with the tick number in a perfectly stationary economy. This one does not: it is the
    /// answer to "how long did a household that got served wait", and it is flat once the
    /// warm-up is over.
    ///
    /// Both are recorded, and neither is enough alone. This one alone would make a cohort that
    /// never gets served look patient; the other alone would report a trend that is arithmetic.
    /// </summary>
    public double WaitMedianMet(Cohort cohort) => cohortWaitMedianMet[(int)cohort];

    public double WaitMedianMet(CohortCell cell) => waitMedianMet[Index(cell)];

    public int Wanted(Cohort cohort, int category) => Sum(wanted, cohort, category);

    public int Wanted(CohortCell cell, int category) => wanted[At(Index(cell), category)];

    public int Obtained(Cohort cohort, int category) => Sum(obtained, cohort, category);

    public int Obtained(CohortCell cell, int category) => obtained[At(Index(cell), category)];

    public Money Spend(Cohort cohort, int category) => Sum(spendByCategory, cohort, category);

    public Money Spend(CohortCell cell, int category) => spendByCategory[At(Index(cell), category)];

    /// <summary>The cohort's `tier_mix`, as counts: units it obtained of this category at this tier.</summary>
    public int Units(Cohort cohort, int category, int tier)
    {
        var total = 0;

        for (var a = 0; a < archetypes; a++)
        {
            total += Units(new CohortCell(cohort, a), category, tier);
        }

        return total;
    }

    public int Units(CohortCell cell, int category, int tier) =>
        units[(Index(cell) * goods.GoodCount) + goods.Index(category, tier)];

    /// <summary>Totals over the categories, for the columns a reader looks at first.</summary>
    public int Wanted(Cohort cohort) => Total(wanted, cohort);

    public int Wanted(CohortCell cell) => Total(wanted, Index(cell));

    public int Obtained(Cohort cohort) => Total(obtained, cohort);

    public int Obtained(CohortCell cell) => Total(obtained, Index(cell));

    // ---- recording ----------------------------------------------------------------------------

    /// <summary>Clears the tick's accumulators. The cohort membership is not one of them.</summary>
    public void OpenTick()
    {
        Array.Clear(wanted);
        Array.Clear(obtained);
        Array.Clear(spendByCategory);
        Array.Clear(units);
        Array.Clear(spend);
        Array.Clear(quality);

        for (var i = 0; i < cells; i++)
        {
            Array.Clear(waits[i]);
            Array.Clear(waitsMet[i]);
        }

        for (var i = 0; i < CohortCount; i++)
        {
            Array.Clear(cohortWaits[i]);
            Array.Clear(cohortWaitsMet[i]);
        }
    }

    /// <summary>Step 3 told this household it wants a unit of this category.</summary>
    internal void RecordWant(int cell, int category) => wanted[At(cell, category)]++;

    /// <summary>The same, addressed by cell rather than by index — for callers not in the tick loop.</summary>
    internal void RecordWant(CohortCell cell, int category) => RecordWant(Index(cell), category);

    /// <summary>The same, addressed by cell rather than by index — for callers not in the tick loop.</summary>
    internal void RecordPurchase(CohortCell cell, int category, int tier, Money paid, int wait) =>
        RecordPurchase(Index(cell), category, tier, paid, wait);

    /// <summary>
    /// The walk finished and this household holds one new unit of the category, at this tier,
    /// having paid the tier's posted price for it. Called once per category per household — the
    /// increments a household walked add up to the posted price of the tier it landed on.
    /// </summary>
    internal void RecordPurchase(int cell, int category, int tier, Money paid, int wait)
    {
        obtained[At(cell, category)]++;
        spendByCategory[At(cell, category)] += paid;
        spend[cell] += paid;
        units[(cell * goods.GoodCount) + goods.Index(category, tier)]++;

        // The **tier table's** value multiplier, not the household's `value_mult^kappa`. This is a
        // reported quantity, and it has to mean the same thing for every household or a cohort's
        // quality index would move when the population's taste changed rather than when what it
        // bought did. What a household thinks a tier is worth belongs in its decision, not here.
        quality[cell] += goods.Tiers[tier].ValueMult;

        if (goods.IsDurable(category))
        {
            RecordWait(waits[cell], wait);
            RecordWait(waitsMet[cell], wait);
            RecordWait(cohortWaits[cell / archetypes], wait);
            RecordWait(cohortWaitsMet[cell / archetypes], wait);
        }
    }

    /// <summary>
    /// Closes the tick: the balances, and the waits of the wants that were **not** met — which are
    /// counted at their current age rather than dropped.
    /// </summary>
    public void Close(Households population, Ledger.Ledger books, LoanBook loans)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(loans);

        Array.Clear(cash);
        Array.Clear(loansOutstanding);
        Array.Clear(debtService);

        for (var h = 0; h < population.Count; h++)
        {
            var cell = Cell(population, h);

            cash[cell] += books.Cash(h);
            loansOutstanding[cell] += loans.OutstandingPrincipal(h);
            debtService[cell] += loans.DebtService(h);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                var i = population.AgeIndex(h, c);

                if (population.Wanted[i] && goods.IsDurable(c))
                {
                    RecordWait(waits[cell], population.Wait[i]);
                    RecordWait(cohortWaits[cell / archetypes], population.Wait[i]);
                }
            }
        }

        for (var cell = 0; cell < cells; cell++)
        {
            waitMedian[cell] = Median(waits[cell]);
            waitMedianMet[cell] = Median(waitsMet[cell]);
        }

        for (var cohort = 0; cohort < CohortCount; cohort++)
        {
            cohortWaitMedian[cohort] = Median(cohortWaits[cohort]);
            cohortWaitMedianMet[cohort] = Median(cohortWaitsMet[cohort]);
        }
    }

    // ---- the median ---------------------------------------------------------------------------

    private static double Median(int[] histogram)
    {
        var total = 0L;

        foreach (var count in histogram)
        {
            total += count;
        }

        if (total == 0)
        {
            return 0.0;
        }

        // The two middle order statistics, averaged — the same answer a sorted list would give,
        // for an even count as well as an odd one.
        var lower = (total - 1) / 2;
        var upper = total / 2;

        return (At(histogram, lower) + At(histogram, upper)) / 2.0;
    }

    private static double At(int[] histogram, long rank)
    {
        var seen = 0L;

        for (var wait = 0; wait < histogram.Length; wait++)
        {
            seen += histogram[wait];

            if (seen > rank)
            {
                return wait;
            }
        }

        return histogram.Length - 1;
    }

    private static void RecordWait(int[] histogram, int wait) =>
        histogram[Math.Min(wait, histogram.Length - 1)]++;

    private static int[][] Histograms(int rows, int ticks)
    {
        var histograms = new int[rows][];

        for (var i = 0; i < rows; i++)
        {
            histograms[i] = new int[ticks + 2];
        }

        return histograms;
    }

    /// <summary>Where a cell lives in the flat arrays. Cohort-major, so a cohort's cells are contiguous.</summary>
    private int Index(CohortCell cell) => ((int)cell.Cohort * archetypes) + cell.Archetype;

    private int At(int cell, int category) => (cell * goods.CategoryCount) + category;

    private int Total(int[] byCategory, Cohort cohort)
    {
        var total = 0;

        for (var a = 0; a < archetypes; a++)
        {
            total += Total(byCategory, Index(new CohortCell(cohort, a)));
        }

        return total;
    }

    private int Total(int[] byCategory, int cell)
    {
        var total = 0;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            total += byCategory[At(cell, c)];
        }

        return total;
    }

    // ---- summing a cohort out of its cells ------------------------------------------------------

    private int Sum(int[] byCell, Cohort cohort)
    {
        var total = 0;

        for (var a = 0; a < archetypes; a++)
        {
            total += byCell[Index(new CohortCell(cohort, a))];
        }

        return total;
    }

    private double Sum(double[] byCell, Cohort cohort)
    {
        var total = 0.0;

        for (var a = 0; a < archetypes; a++)
        {
            total += byCell[Index(new CohortCell(cohort, a))];
        }

        return total;
    }

    private Money Sum(Money[] byCell, Cohort cohort)
    {
        var total = Money.Zero;

        for (var a = 0; a < archetypes; a++)
        {
            total += byCell[Index(new CohortCell(cohort, a))];
        }

        return total;
    }

    private int Sum(int[] byCellAndCategory, Cohort cohort, int category)
    {
        var total = 0;

        for (var a = 0; a < archetypes; a++)
        {
            total += byCellAndCategory[At(Index(new CohortCell(cohort, a)), category)];
        }

        return total;
    }

    private Money Sum(Money[] byCellAndCategory, Cohort cohort, int category)
    {
        var total = Money.Zero;

        for (var a = 0; a < archetypes; a++)
        {
            total += byCellAndCategory[At(Index(new CohortCell(cohort, a)), category)];
        }

        return total;
    }
}
