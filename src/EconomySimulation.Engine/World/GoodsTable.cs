using EconomySimulation.Engine.Configuration;

namespace EconomySimulation.Engine.World;

/// <summary>
/// The goods of the town: six rows as data, eighteen shelves as a consequence.
///
/// **A row is a good.** `01-SIMULATION.md` §5.5 also calls it a product group; §3.1 called it a
/// category, because there every good was the only good in its category, and <see cref="Categories"/>
/// keeps that spelling for the same reason the TOML section does. What a row is grouped *under* is
/// its <see cref="CategoryParameters.Label"/>, and <see cref="Labels"/> is the list of those — six
/// of them under §3.1, six under §3.6's eighteen goods. A shelf is a (row, tier) pair, which is why
/// the count of them is <see cref="ShelfCount"/> and not something with "good" in the name.
///
/// Nothing here is stored per category and per tier that could be derived from a category and a
/// tier. A tier's opening price is `price_ref · price_mult`, its value multiplier is the tier's,
/// and its supply is the category's capacity split by unit share. The tier system stays ten
/// numbers rather than eighteen rows, and adding a category adds a row rather than a branch —
/// there is no enum of categories anywhere in the engine, and GoodsTableTests checks that.
/// </summary>
public sealed class GoodsTable
{
    private readonly Money[] openingPrices;
    private readonly int[] units;
    private readonly int[] labelOf;
    private readonly Money[] floors;
    private readonly double[] incomeSlopes;

    public GoodsTable(SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Anything wrong with the parameters themselves is the loader's business and has already
        // been reported; what is left here would be a bug in the caller.
        if (parameters.Categories.Count == 0 || parameters.Tiers.Count == 0)
        {
            throw new ArgumentException(
                "A goods table needs at least one category and at least one tier.",
                nameof(parameters));
        }

        Categories = parameters.Categories;
        Tiers = parameters.Tiers;
        MeanIncome = parameters.Income.MeanIncome;

        openingPrices = new Money[ShelfCount];
        units = new int[ShelfCount];
        floors = new Money[CategoryCount];
        incomeSlopes = new double[CategoryCount];
        labelOf = new int[CategoryCount];

        var labels = new List<string>();

        for (var c = 0; c < CategoryCount; c++)
        {
            var label = Categories[c].Label;
            var at = labels.IndexOf(label);

            if (at < 0)
            {
                at = labels.Count;
                labels.Add(label);
            }

            labelOf[c] = at;
        }

        Labels = labels;

        var shares = Tiers.Select(t => t.UnitShare).ToArray();

        for (var c = 0; c < CategoryCount; c++)
        {
            var category = Categories[c];

            // The Stone-Geary split, computed and never typed in. It is exactly neutral at the
            // mean income — a_g + b_g · mean == v_g · mean — so necessity_g changes the income
            // gradient of demand and nothing else.
            floors[c] = MeanIncome.Scaled(category.Necessity * category.V);
            incomeSlopes[c] = (1.0 - category.Necessity) * category.V;

            // Capacity split by units, not by value. That is what keeps total units at capacity,
            // so unit demand and unit supply match by construction; and because the unit shares
            // times the price multipliers sum to 1, the capacity *value* is unchanged by the tier
            // system too. Splitting by value instead breaks both, and the symptom turns up fifty
            // ticks later as a draining pool.
            var perTier = Allocation.LargestRemainder(category.Capacity, shares);

            for (var t = 0; t < TierCount; t++)
            {
                openingPrices[Index(c, t)] = category.PriceRef.Scaled(Tiers[t].PriceMult);
                units[Index(c, t)] = (int)perTier[t];
            }
        }
    }

    public IReadOnlyList<CategoryParameters> Categories { get; }

    public IReadOnlyList<TierParameters> Tiers { get; }

    public Money MeanIncome { get; }

    public int CategoryCount => Categories.Count;

    public int TierCount => Tiers.Count;

    /// <summary>Every shelf in the town: one per row per tier. Eighteen under §3.1, fifty-four under §3.6.</summary>
    public int ShelfCount => CategoryCount * TierCount;

    /// <summary>
    /// The distinct category labels, in the order their first row appears.
    ///
    /// First-appearance order rather than alphabetical, because it is the order the goods table
    /// states, and the output columns derived from it should read the way the calibration does.
    /// Under §3.1 this is the six row names again, which is what keeps that output unchanged.
    /// </summary>
    public IReadOnlyList<string> Labels { get; }

    /// <summary>Which entry of <see cref="Labels"/> a row rolls up into.</summary>
    public int LabelOf(int category) => labelOf[category];

    /// <summary>
    /// Where a (category, tier) pair lives in the flat arrays. Category-major, so a household
    /// walking one category's tiers walks contiguous memory.
    /// </summary>
    public int Index(int category, int tier) => (category * TierCount) + tier;

    public Money OpeningPrice(int category, int tier) => openingPrices[Index(category, tier)];

    public int Units(int category, int tier) => units[Index(category, tier)];

    /// <summary>`a_g` — the part of a category's value that does not scale with income.</summary>
    public Money Floor(int category) => floors[category];

    /// <summary>`b_g` — the part that does.</summary>
    public double IncomeSlope(int category) => incomeSlopes[category];

    /// <summary>
    /// Whether a category is a durable, computed rather than labelled. Membership of the grids
    /// later experiments cut the results by — durable or not, financeable or not — is a fact
    /// about the numbers, and a stored label is a second copy of it that can disagree.
    /// </summary>
    public bool IsDurable(int category) => Categories[category].Life > 1;

    public bool IsFinanceable(int category) => Categories[category].Financeable;

    /// <summary>
    /// What the town produces in a tick, valued at opening prices.
    ///
    /// This is the identity the pool rests on. Because the unit shares times the price multipliers
    /// sum to 1.000, it is the same whatever the tier mix, so whenever the market clears, nominal
    /// output equals nominal income.
    /// </summary>
    public Money CapacityValue
    {
        get
        {
            var total = Money.Zero;

            for (var c = 0; c < CategoryCount; c++)
            {
                for (var t = 0; t < TierCount; t++)
                {
                    total += OpeningPrice(c, t) * Units(c, t);
                }
            }

            return total;
        }
    }

    /// <summary>
    /// What one household's standard-tier basket costs per tick: `Σ price_ref_g / life_g`.
    ///
    /// The headline calibration identity — it comes to the mean income, which is why a median
    /// household can afford roughly the standard basket and no more.
    /// </summary>
    public double StandardBasketFlowCost
    {
        get
        {
            var total = 0.0;

            for (var c = 0; c < CategoryCount; c++)
            {
                total += Categories[c].PriceRef.Cents / 100.0 / Categories[c].Life;
            }

            return total;
        }
    }
}
