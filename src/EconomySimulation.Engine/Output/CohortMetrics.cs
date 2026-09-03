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
/// </summary>
public sealed class CohortMetrics
{
    private const int Count = 2;

    private readonly GoodsTable goods;
    private readonly int[] households = new int[Count];

    // Per cohort.
    private readonly Money[] cash = new Money[Count];
    private readonly Money[] loansOutstanding = new Money[Count];
    private readonly Money[] debtService = new Money[Count];
    private readonly Money[] spend = new Money[Count];
    private readonly double[] quality = new double[Count];
    private readonly double[] waitMedian = new double[Count];
    private readonly double[] waitMedianMet = new double[Count];
    private readonly int[][] waits;
    private readonly int[][] waitsMet;

    // Per cohort, per category.
    private readonly int[] wanted;
    private readonly int[] obtained;
    private readonly Money[] spendByCategory;

    // Per cohort, per category, per tier.
    private readonly int[] units;

    public CohortMetrics(GoodsTable goods, Households population, int ticks)
    {
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(population);
        ArgumentOutOfRangeException.ThrowIfLessThan(ticks, 1);

        this.goods = goods;

        wanted = new int[Count * goods.CategoryCount];
        obtained = new int[Count * goods.CategoryCount];
        spendByCategory = new Money[Count * goods.CategoryCount];
        units = new int[Count * goods.GoodCount];

        // A histogram rather than a list: the median has to be computed every tick inside a run
        // that allocates nothing, and a wait is a small non-negative integer. The last bucket
        // holds everything at or beyond the run's length, which nothing can exceed.
        waits = [new int[ticks + 2], new int[ticks + 2]];
        waitsMet = [new int[ticks + 2], new int[ticks + 2]];

        for (var h = 0; h < population.Count; h++)
        {
            households[(int)Of(population, h)]++;
        }
    }

    /// <summary>Which cohort a household is in. By construction, never changes and never depends on the scenario.</summary>
    public static Cohort Of(Households population, int household)
    {
        ArgumentNullException.ThrowIfNull(population);

        return population.IsAbstainer[household] ? Cohort.Abstainer : Cohort.Borrower;
    }

    public int Households(Cohort cohort) => households[(int)cohort];

    public Money Cash(Cohort cohort) => cash[(int)cohort];

    public Money LoansOutstanding(Cohort cohort) => loansOutstanding[(int)cohort];

    public Money DebtService(Cohort cohort) => debtService[(int)cohort];

    /// <summary>What the cohort spent on goods this tick, at the posted prices of the tiers it landed on.</summary>
    public Money Spend(Cohort cohort) => spend[(int)cohort];

    /// <summary>
    /// Units obtained this tick weighted by `value_mult`, so a cohort that holds its unit count by
    /// buying worse goods is not recorded as unaffected. Raw, not per household: the cohort sizes
    /// are in the same file.
    /// </summary>
    public double Quality(Cohort cohort) => quality[(int)cohort];

    /// <summary>
    /// The median wait, in ticks, over every **durable** want the cohort faced this tick — those
    /// met, at the wait they were met after, and those still open, at their current age.
    ///
    /// Counting the unmet ones is not a detail. Under `credit_high` the abstainers most affected
    /// are exactly the ones who never get served, and a median over completed waits alone would
    /// drop them and report an improvement. Food and leisure are excluded because they are wanted
    /// every tick and met or not the same tick, so their zeros would swamp the number that matters.
    /// </summary>
    public double WaitMedian(Cohort cohort) => waitMedian[(int)cohort];

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
    public double WaitMedianMet(Cohort cohort) => waitMedianMet[(int)cohort];

    public int Wanted(Cohort cohort, int category) => wanted[At(cohort, category)];

    public int Obtained(Cohort cohort, int category) => obtained[At(cohort, category)];

    public Money Spend(Cohort cohort, int category) => spendByCategory[At(cohort, category)];

    /// <summary>The cohort's `tier_mix`, as counts: units it obtained of this category at this tier.</summary>
    public int Units(Cohort cohort, int category, int tier) =>
        units[((int)cohort * goods.GoodCount) + goods.Index(category, tier)];

    /// <summary>Totals over the categories, for the columns a reader looks at first.</summary>
    public int Wanted(Cohort cohort) => Total(wanted, cohort);

    public int Obtained(Cohort cohort) => Total(obtained, cohort);

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

        for (var i = 0; i < Count; i++)
        {
            Array.Clear(waits[i]);
            Array.Clear(waitsMet[i]);
        }
    }

    /// <summary>Step 3 told this household it wants a unit of this category.</summary>
    internal void RecordWant(Cohort cohort, int category) => wanted[At(cohort, category)]++;

    /// <summary>
    /// The walk finished and this household holds one new unit of the category, at this tier,
    /// having paid the tier's posted price for it. Called once per category per household — the
    /// increments a household walked add up to the posted price of the tier it landed on.
    /// </summary>
    internal void RecordPurchase(Cohort cohort, int category, int tier, Money paid, int wait)
    {
        obtained[At(cohort, category)]++;
        spendByCategory[At(cohort, category)] += paid;
        spend[(int)cohort] += paid;
        units[((int)cohort * goods.GoodCount) + goods.Index(category, tier)]++;
        quality[(int)cohort] += goods.Tiers[tier].ValueMult;

        if (goods.IsDurable(category))
        {
            RecordWait(waits[(int)cohort], wait);
            RecordWait(waitsMet[(int)cohort], wait);
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
            var cohort = (int)Of(population, h);

            cash[cohort] += books.Cash(h);
            loansOutstanding[cohort] += loans.OutstandingPrincipal(h);
            debtService[cohort] += loans.DebtService(h);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                var i = population.AgeIndex(h, c);

                if (population.Wanted[i] && goods.IsDurable(c))
                {
                    RecordWait(waits[cohort], population.Wait[i]);
                }
            }
        }

        for (var cohort = 0; cohort < Count; cohort++)
        {
            waitMedian[cohort] = Median(waits[cohort]);
            waitMedianMet[cohort] = Median(waitsMet[cohort]);
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

    private int At(Cohort cohort, int category) => ((int)cohort * goods.CategoryCount) + category;

    private int Total(int[] byCategory, Cohort cohort)
    {
        var total = 0;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            total += byCategory[At(cohort, c)];
        }

        return total;
    }
}
