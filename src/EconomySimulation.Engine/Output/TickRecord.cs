using EconomySimulation.Engine.World;

namespace EconomySimulation.Engine.Output;

/// <summary>
/// What one tick recorded — the prices it traded at, and the aggregates measured at its end.
///
/// It exists because two of the numbers cannot be read off the model afterwards. Repricing (step 6)
/// runs before the tick ends, so <see cref="Market"/> holds *next* tick's prices by the time
/// anything could ask; and restocking clears the demand counters at the start of the next tick. The
/// record is filled at the two moments the answers are still true, and the writer reads it.
///
/// Nothing in the model reads this. It is an output, and outputs that feed back into decisions stop
/// being measurements.
/// </summary>
public sealed class TickRecord
{
    private readonly Money[] pricesTraded;

    public TickRecord(GoodsTable goods)
    {
        ArgumentNullException.ThrowIfNull(goods);

        Goods = goods;
        pricesTraded = new Money[goods.GoodCount];
    }

    public GoodsTable Goods { get; }

    /// <summary>The tick this record describes. −1 before the first one opens.</summary>
    public int Tick { get; private set; } = -1;

    /// <summary>Whether the tick falls in the warm-up. Warm-up is written and flagged, never discarded.</summary>
    public bool IsWarmup { get; private set; }

    public Money MoneyStock { get; private set; }

    public Money LoansOutstanding { get; private set; }

    public Money Pool { get; private set; }

    public Money MoneyCreated { get; private set; }

    public Money MoneyDestroyed { get; private set; }

    /// <summary>Live loans at the end of the tick.</summary>
    public int LoansLive { get; private set; }

    /// <summary>Loans the pool could not fund, with `money_creation` off.</summary>
    public int Rationed { get; private set; }

    /// <summary>The price the tick actually traded at, before step 6 moved it.</summary>
    public Money PriceTraded(int category, int tier) => pricesTraded[Goods.Index(category, tier)];

    /// <summary>Called at the top of a tick, once the shelves are stocked and before anything decides.</summary>
    public void Open(int tick, Market market, int warmupTicks)
    {
        ArgumentNullException.ThrowIfNull(market);

        Tick = tick;
        IsWarmup = tick <= warmupTicks;

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            for (var t = 0; t < Goods.TierCount; t++)
            {
                pricesTraded[Goods.Index(c, t)] = market.Price(c, t);
            }
        }
    }

    /// <summary>Called at the end of a tick, after the check has passed.</summary>
    public void Close(Ledger.Ledger books, Credit.LoanBook loans, int rationed)
    {
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(loans);

        MoneyStock = books.MoneyHeld;
        LoansOutstanding = books.LoansOutstanding;
        Pool = books.Pool;
        MoneyCreated = books.Created;
        MoneyDestroyed = books.Destroyed;
        LoansLive = loans.LiveCount;
        Rationed = rationed;
    }
}
