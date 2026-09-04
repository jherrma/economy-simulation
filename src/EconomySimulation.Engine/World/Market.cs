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
    private readonly double[] priceFactor;

    public Market(GoodsTable goods)
    {
        ArgumentNullException.ThrowIfNull(goods);

        this.goods = goods;

        prices = new Money[goods.ShelfCount];
        stock = new int[goods.ShelfCount];
        sold = new int[goods.ShelfCount];
        blocked = new int[goods.ShelfCount];
        unaffordable = new int[goods.ShelfCount];
        priceFactor = new double[goods.ShelfCount];

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
                priceFactor[goods.Index(c, t)] = 1.0;
            }
        }

        Restock();
    }

    public Money Price(int category, int tier) => prices[goods.Index(category, tier)];

    /// <summary>
    /// Every posted price, indexed by <see cref="GoodsTable.Index"/>. A span rather than a lookup
    /// function, because the price index is computed inside the tick and a delegate handed to it
    /// would be an allocation per tick.
    /// </summary>
    public ReadOnlySpan<Money> Prices => prices;

    public int Stock(int category, int tier) => stock[goods.Index(category, tier)];

    public int Sold(int category, int tier) => sold[goods.Index(category, tier)];

    public int Blocked(int category, int tier) => blocked[goods.Index(category, tier)];

    public int Unaffordable(int category, int tier) => unaffordable[goods.Index(category, tier)];

    /// <summary>
    /// What would have sold with unlimited stock: `sold + blocked`. **`unaffordable` is not
    /// demand.** Counting it raises prices on goods nobody can buy, which makes more households
    /// unable to buy them; leaving `blocked` out means demand can never exceed supply and prices
    /// only ever fall. Both failures produce a clean series that looks like a finding (05-01).
    /// </summary>
    public int Demand(int category, int tier) => sold[goods.Index(category, tier)] + blocked[goods.Index(category, tier)];

    /// <summary>
    /// Step 6 — every shelf reprices on its own excess demand, and only its own:
    ///
    /// <code>
    /// price ← price · (1 + k · clamp((D − units) / units, −1, +1)),  then max(price, price_floor)
    /// </code>
    ///
    /// The posted price is carried as a dimensionless factor on the opening price and rounded to
    /// the cent from there each tick, rather than rounded and re-rounded in place. Cent rounding
    /// cannot commute with scaling every nominal quantity by `c` (V3), but this way the discrepancy
    /// is bounded at half a cent per posting instead of compounding over 360 ticks. New prices
    /// apply from the next tick: this runs after the walk and before the check, and prices are
    /// constant through a walk (TickStep).
    ///
    /// Each tier moving on its own signal is what lets relative tier prices move, and that
    /// movement is the trade-down channel. A category-wide signal would freeze the tier mix at
    /// the unit shares and answer the question by assumption.
    /// </summary>
    public void Reprice(
        double k,
        Money floor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(k);

        for (var c = 0; c < goods.CategoryCount; c++)
        {

            for (var t = 0; t < goods.TierCount; t++)
            {
                var i = goods.Index(c, t);
                var units = goods.Units(c, t);
                var opening = goods.OpeningPrice(c, t);

                // A shelf with no supply has no signal; its price stands.
                if (units == 0)
                {
                    continue;
                }

                var excess = Math.Clamp((Demand(c, t) - units) / (double)units, -1.0, 1.0);
                priceFactor[i] *= 1.0 + (k * excess);

                var posted = opening.Scaled(priceFactor[i]);

                if (posted < floor)
                {
                    posted = floor;
                    priceFactor[i] = floor.Cents / (double)opening.Cents;
                }

                prices[i] = posted;
            }
        }
    }

    /// <summary>
    /// One unit leaves the shelf. Called once per household per category, at the tier the
    /// household finally lands on: a household that walked budget → standard has consumed one
    /// standard unit, not one of each.
    /// </summary>
    internal void Sell(int category, int tier)
    {
        var i = goods.Index(category, tier);

        if (stock[i] <= 0)
        {
            throw new InvalidOperationException(
                $"Selling from an empty shelf ({category}, {tier}) — the walk checked stock before taking.");
        }

        stock[i]--;
        sold[i]++;
    }

    /// <summary>Willing and able, and the shelf was empty. This is demand (05-01).</summary>
    internal void RecordBlocked(int category, int tier) => blocked[goods.Index(category, tier)]++;

    /// <summary>Willing, and could not pay. This is not demand (05-01).</summary>
    internal void RecordUnaffordable(int category, int tier) => unaffordable[goods.Index(category, tier)]++;

    /// <summary>
    /// Posts a new price on one shelf. Repricing (05-02) is the only caller in a run; tests use it
    /// to put the shelves into states a run reaches only after many ticks.
    /// </summary>
    internal void SetPrice(int category, int tier, Money price)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(price.Cents, 0);

        prices[goods.Index(category, tier)] = price;
        priceFactor[goods.Index(category, tier)] = price.Cents / (double)goods.OpeningPrice(category, tier).Cents;
    }

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
