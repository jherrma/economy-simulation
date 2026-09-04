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
    private readonly double[] categoryIndices;
    private readonly double[] labelIndices;

    public TickRecord(GoodsTable goods)
    {
        ArgumentNullException.ThrowIfNull(goods);

        Goods = goods;
        pricesTraded = new Money[goods.ShelfCount];
        categoryIndices = new double[goods.CategoryCount];
        labelIndices = new double[goods.Labels.Count];
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

    /// <summary>
    /// The Laspeyres index on the fixed supply basket, at the prices this tick traded at. Exactly
    /// 1 in the tick that trades at the opening prices, and an output that nothing in the model
    /// reads (<see cref="PriceIndex"/>).
    /// </summary>
    public double Cpi { get; private set; }

    /// <summary>One row's price index, on the same basis.</summary>
    public double CategoryIndex(int category) => categoryIndices[category];

    /// <summary>One category label's price index — the unit-weighted roll-up of its rows.</summary>
    public double LabelIndex(int label) => labelIndices[label];

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

        // Computed here rather than at the end of the tick, because it is an index of the prices
        // the tick *traded at*, which step 6 is about to change.
        Cpi = PriceIndex.Cpi(Goods, pricesTraded);

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            categoryIndices[c] = PriceIndex.ForCategory(Goods, c, pricesTraded);
        }

        for (var l = 0; l < labelIndices.Length; l++)
        {
            labelIndices[l] = PriceIndex.ForLabel(Goods, l, pricesTraded);
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
