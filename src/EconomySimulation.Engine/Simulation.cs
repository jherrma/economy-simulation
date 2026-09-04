using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.Output;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Engine;

/// <summary>
/// One run of the town: a population, a ledger, a market, and the tick that acts on them.
///
/// Every step of the tick exists and is called in order from the first commit, so that later
/// stories fill a step in rather than deciding where their logic belongs. A step that does nothing
/// yet is a named no-op, not an absence.
/// </summary>
public sealed class Simulation
{
    /// <summary>
    /// The order, as data. `spec/tick-order.txt` holds the same list, and a test compares them —
    /// so adding or reordering a step is a deliberate edit to a committed file with a reason
    /// attached, rather than a line moved in a method.
    /// </summary>
    public static IReadOnlyList<TickStep> StepOrder { get; } =
    [
        TickStep.Income,
        TickStep.DebtService,
        TickStep.Wants,
        TickStep.Walk,
        TickStep.Ageing,
        TickStep.Repricing,
        TickStep.Check,
    ];

    public Simulation(SimulationParameters parameters, int runSeed)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        Parameters = parameters;
        RunSeed = runSeed;

        Goods = new GoodsTable(parameters);
        Population = Households.Draw(parameters, Goods, runSeed);
        Market = new Market(Goods);
        Books = Ledger.Ledger.Open(
            Population.OpeningCash(parameters.Income.OpeningCashShare),
            OpeningPool(parameters, Population));
        Loans = new LoanBook(Population, LoanCapacity(Goods, Population.Count));
        Shopping = new Walker(parameters, Goods, Market, Population, Books, Loans, runSeed);
        Recorded = new TickRecord(Goods);
        Cohorts = new CohortMetrics(Goods, Population, parameters.Run.Ticks);
        Shopping.Cohorts = Cohorts;
    }

    public SimulationParameters Parameters { get; }

    public int RunSeed { get; }

    public GoodsTable Goods { get; }

    public Households Population { get; }

    public Market Market { get; }

    public Ledger.Ledger Books { get; }

    public Walker Shopping { get; }

    /// <summary>Every live loan. Empty for the whole run with credit off.</summary>
    public LoanBook Loans { get; }

    /// <summary>
    /// What the last tick recorded. Filled at the two moments the answers are still true — the
    /// prices before step 6 moves them, the aggregates after step 7 has passed — and read only by
    /// the writer. Nothing in the model reads it.
    /// </summary>
    public TickRecord Recorded { get; }

    /// <summary>The abstainer and borrower series for the last tick — the finding itself (07-03).</summary>
    public CohortMetrics Cohorts { get; }

    /// <summary>The last completed tick. −1 before the run starts.</summary>
    public int Tick { get; private set; } = -1;

    /// <summary>Set by tests to record the steps a tick actually ran, in the order it ran them.</summary>
    internal Action<TickStep>? StepObserver { get; set; }

    /// <summary>
    /// The opening state, checked before the first tick rather than after it.
    ///
    /// V1 holds here trivially — nothing has moved — which is the point: if it does not, the
    /// opening balance sheet is wrong and no amount of later checking will say so clearly.
    /// </summary>
    public Result Start()
    {
        var totalIncome = Money.Zero;
        for (var h = 0; h < Population.Count; h++)
        {
            totalIncome += Population.Income[h];
        }

        // The pool is a buffer that makes the warm-up transient survivable; less than one tick of
        // income and the run halts almost immediately, for a reason that would look economic.
        if (Books.Pool < totalIncome)
        {
            return Result.Fail(
                $"the seller pool: expected at least one tick of total income ({totalIncome.ToCsv()}), "
                + $"got {Books.Pool.ToCsv()} — the run would halt within a few ticks");
        }

        if (!Books.LoansOutstanding.IsZero)
        {
            return Result.Fail(
                $"loans outstanding at t = 0: expected 0.00, got {Books.LoansOutstanding.ToCsv()}");
        }

        return Books.Check(tick: 0, Parameters.Credit.MoneyCreation);
    }

    /// <summary>Runs the whole configured span, stopping at the first failed check.</summary>
    public Result Run()
    {
        var opening = Start();

        if (opening.IsFailed)
        {
            return opening;
        }

        for (var tick = 1; tick <= Parameters.Run.Ticks; tick++)
        {
            var result = RunTick(tick);

            if (result.IsFailed)
            {
                return result;
            }
        }

        return Results.Ok;
    }

    /// <summary>
    /// One tick: restock, then the seven steps in order.
    ///
    /// Restocking is not one of the steps because nothing decides and nothing moves — it is the
    /// definition of the shelf the steps then act on (`01-SIMULATION.md` §6).
    /// </summary>
    public Result RunTick(int tick)
    {
        Market.Restock();
        Books.OpenTick();
        Recorded.Open(tick, Market, Parameters.Run.WarmupTicks);
        Cohorts.OpenTick();

        // Indexed, not foreach: enumerating through the interface boxes an enumerator, and the
        // tick allocates nothing.
        for (var i = 0; i < StepOrder.Count; i++)
        {
            var step = StepOrder[i];
            StepObserver?.Invoke(step);

            var result = Run(step, tick);

            if (!Results.IsOk(result))
            {
                return result;
            }
        }

        Recorded.Close(Books, Loans, Shopping.Rationed);
        Cohorts.Close(Population, Books, Loans);
        Tick = tick;

        return Results.Ok;
    }

    // CS8524 only: adding a step must break this build, which is CS8509 and stays armed.
#pragma warning disable CS8524
    private Result Run(TickStep step, int tick) => step switch
    {
        TickStep.Income => Income(tick),
        TickStep.DebtService => DebtService(tick),
        TickStep.Wants => Wants(tick),
        TickStep.Walk => Walk(tick),
        TickStep.Ageing => Ageing(tick),
        TickStep.Repricing => Repricing(tick),
        TickStep.Check => Books.Check(tick, Parameters.Credit.MoneyCreation),
    };
#pragma warning restore CS8524

    /// <summary>
    /// Step 1 — each household receives `income_h` from the pool. No story owned this step; it
    /// is filled in with the walk (04-05) because the walk is the first thing that needs a budget
    /// to replenish. A pool that cannot pay is the calibration failure §6 step 7 describes, and
    /// the transfer's own refusal reports it.
    /// </summary>
    private Result Income(int tick)
    {
        for (var h = 0; h < Population.Count; h++)
        {
            var paid = Books.Transfer(Account.Pool, Account.Household(h), Population.Income[h], TransferReason.Income);

            if (!Results.IsOk(paid))
            {
                // A fresh error with the transfer's own refusal as its cause; the result is built
                // once and never mutated afterwards.
                var error = new Error(
                    $"income, tick {tick}: the pool cannot pay household {h}. This is a calibration "
                    + "result, not a bug: income exceeds what the economy can pay. Report it as a "
                    + "finding about the parameters.").CausedBy(paid.Errors);

                return Result.Fail(error);
            }
        }

        return Results.Ok;
    }

    /// <summary>Step 2 — every instalment due, before any shopping; principal destroyed, interest back out as the dividend.</summary>
    private Result DebtService(int tick)
    {
        _ = tick;

        return Loans.Service(Books, Parameters.Credit.MoneyCreation);
    }

    /// <summary>Step 3 — wants, in units, never budgets. One unit of a category at most.</summary>
    private Result Wants(int tick)
    {
        _ = tick;

        for (var h = 0; h < Population.Count; h++)
        {
            var cell = CohortMetrics.Cell(Population, h);

            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                Population.RefreshWant(h, c, Goods.Categories[c].Life);

                if (Population.Wanted[Population.AgeIndex(h, c)])
                {
                    Cohorts.RecordWant(cell, c);
                }
            }
        }

        return Results.Ok;
    }

    /// <summary>Step 4 — the shopping walk.</summary>
    private Result Walk(int tick) => Shopping.Run(tick);

    /// <summary>Step 5 — every held durable gets a tick older.</summary>
    private Result Ageing(int tick)
    {
        _ = tick;
        Population.AgeDurables(Goods);

        return Results.Ok;
    }

    /// <summary>Step 6 — eighteen prices, each on its own excess demand, applying from the next tick.</summary>
    private Result Repricing(int tick)
    {
        _ = tick;
        Market.Reprice(Parameters.Prices.K, Parameters.Prices.PriceFloor);

        return Results.Ok;
    }

    /// <summary>
    /// One slot per tier per financeable category per household: the most a household can hold
    /// while no loan's term exceeds its good's life, so the book never grows in a default run.
    /// </summary>
    private static int LoanCapacity(GoodsTable goods, int households)
    {
        var financeable = 0;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            if (goods.IsFinanceable(c))
            {
                financeable++;
            }
        }

        return Math.Max(1, households * financeable * goods.TierCount);
    }

    private static Money OpeningPool(SimulationParameters parameters, Households population)
    {
        var totalIncome = Money.Zero;

        for (var h = 0; h < population.Count; h++)
        {
            totalIncome += population.Income[h];
        }

        return totalIncome * parameters.Money.OpeningPoolMonths;
    }
}
