namespace EconomySimulation.Engine.World;

/// <summary>
/// The eighteen shelves: a posted price and a stock for every tier of every category, and the
/// demand each one saw this tick.
///
/// Prices change only between ticks. That matters because the walk visits households in sequence:
/// if a stockout moved a price mid-walk, the household visited first would face a different price
/// from the one visited last, and the rationing order would silently become a price advantage.
/// </summary>
public sealed class Market
{
    private readonly GoodsTable goods;
    private readonly Money[] prices;
    private readonly int[] stock;
    private readonly int[] sold;
    private readonly int[] blocked;
    private readonly int[] unaffordable;

    public Market(GoodsTable goods)
    {
        ArgumentNullException.ThrowIfNull(goods);

        this.goods = goods;

        prices = new Money[goods.GoodCount];
        stock = new int[goods.GoodCount];
        sold = new int[goods.GoodCount];
        blocked = new int[goods.GoodCount];
        unaffordable = new int[goods.GoodCount];

        // Opening prices are price_ref · price_mult, and none of them is tuned. At t = 0 the
        // premium tiers sit in heavy surplus, because supply is 40/40/20 while most households
        // want budget or standard. That is deliberate: the realised tier mix is the output the
        // whole experiment turns on, and choosing opening prices to produce a particular mix
        // would be choosing the answer.
        for (var c = 0; c < goods.CategoryCount; c++)
        {
            for (var t = 0; t < goods.TierCount; t++)
            {
                prices[goods.Index(c, t)] = goods.OpeningPrice(c, t);
            }
        }

        Restock();
    }

    public Money Price(int category, int tier) => prices[goods.Index(category, tier)];

    public int Stock(int category, int tier) => stock[goods.Index(category, tier)];

    public int Sold(int category, int tier) => sold[goods.Index(category, tier)];

    public int Blocked(int category, int tier) => blocked[goods.Index(category, tier)];

    public int Unaffordable(int category, int tier) => unaffordable[goods.Index(category, tier)];

    /// <summary>
    /// Sets every shelf back to `units(g,t)` and clears the tick's demand counters.
    ///
    /// This is what "fixed supply per tick" means. Unsold premium units do not pile up into a
    /// glut; they were production that nobody took.
    /// </summary>
    public void Restock()
    {
        for (var c = 0; c < goods.CategoryCount; c++)
        {
            for (var t = 0; t < goods.TierCount; t++)
            {
                stock[goods.Index(c, t)] = goods.Units(c, t);
            }
        }

        Array.Clear(sold);
        Array.Clear(blocked);
        Array.Clear(unaffordable);
    }
}
