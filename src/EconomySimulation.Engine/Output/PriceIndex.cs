using EconomySimulation.Engine.World;

namespace EconomySimulation.Engine.Output;

/// <summary>
/// A Laspeyres price index on the **fixed unit supply** basket:
///
/// <code>
/// cpi_t = Σ_g price_(g,t) · units_g  /  Σ_g price_(g,0) · units_g
/// </code>
///
/// Since supply is fixed by construction, that basket is the only one in this model that cannot be
/// argued with, and fixing it at the supply units rather than at realised purchases is what keeps
/// this a **price** index. A basket tracking what households actually bought would fall when they
/// trade down — and trading down is precisely the movement the index has to stay neutral about,
/// because it is the finding. A town that keeps its prices flat by buying worse goods must not be
/// recorded as unchanged, which is why the realised tier mix is recorded beside this and in the
/// same story.
///
/// It is an **output, never an input**. Nothing in the decision rule reads it, λ is never indexed,
/// and the score is homogeneous of degree zero — so a run at twice every nominal quantity makes
/// exactly the same choices and reports a CPI twice as large.
/// </summary>
public static class PriceIndex
{
    /// <summary>
    /// The whole-basket index. <paramref name="traded"/> holds the price each good traded at this
    /// tick — the price before step 6 moved it — indexed by <see cref="GoodsTable.Index"/>.
    /// </summary>
    public static double Cpi(GoodsTable goods, ReadOnlySpan<Money> traded)
    {
        ArgumentNullException.ThrowIfNull(goods);

        var now = 0L;
        var opening = 0L;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            for (var t = 0; t < goods.TierCount; t++)
            {
                var units = goods.Units(c, t);

                now = checked(now + (traded[goods.Index(c, t)].Cents * units));
                opening = checked(opening + (goods.OpeningPrice(c, t).Cents * units));
            }
        }

        return Ratio(now, opening);
    }

    /// <summary>
    /// One category label's index — the same basket, restricted to the shelves of every row
    /// carrying that label.
    ///
    /// **Unit-weighted, and that is a choice worth stating.** The weights are the supply units, so
    /// a category's index is the roll-up of its goods' indices weighted by opening value, and a
    /// €1,440 washing machine replaced every twelve years counts for its seven units rather than
    /// for its price. Value-weighting instead would let one expensive, rarely-replaced good speak
    /// for a category of five, and the two diverge sharply on exactly the categories §3.6 splits.
    ///
    /// Under §3.1 every row is its own category and this is <see cref="ForCategory"/> again, which
    /// is what makes the roll-up checkable against a table where it cannot be wrong.
    /// </summary>
    public static double ForLabel(GoodsTable goods, int label, ReadOnlySpan<Money> traded)
    {
        ArgumentNullException.ThrowIfNull(goods);

        var now = 0L;
        var opening = 0L;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            if (goods.LabelOf(c) != label)
            {
                continue;
            }

            for (var t = 0; t < goods.TierCount; t++)
            {
                var units = goods.Units(c, t);

                now = checked(now + (traded[goods.Index(c, t)].Cents * units));
                opening = checked(opening + (goods.OpeningPrice(c, t).Cents * units));
            }
        }

        return Ratio(now, opening);
    }

    /// <summary>One row's index, on the same basis and over that row's three tiers.</summary>
    public static double ForCategory(GoodsTable goods, int category, ReadOnlySpan<Money> traded)
    {
        ArgumentNullException.ThrowIfNull(goods);

        var now = 0L;
        var opening = 0L;

        for (var t = 0; t < goods.TierCount; t++)
        {
            var units = goods.Units(category, t);

            now = checked(now + (traded[goods.Index(category, t)].Cents * units));
            opening = checked(opening + (goods.OpeningPrice(category, t).Cents * units));
        }

        return Ratio(now, opening);
    }

    /// <summary>
    /// The realised tier mix: this tier's share of the row's sales this tick. Zero sales in a row
    /// is reported as a zero share rather than as nothing, so the series has a value at every tick
    /// and a reader never has to guess what a gap meant.
    /// </summary>
    public static double MixShare(Market market, GoodsTable goods, int category, int tier)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(goods);

        var sold = 0;

        for (var t = 0; t < goods.TierCount; t++)
        {
            sold += market.Sold(category, t);
        }

        return sold == 0 ? 0.0 : market.Sold(category, tier) / (double)sold;
    }

    /// <summary>
    /// The same share taken **within the category label** rather than within the row: this tier's
    /// share of everything sold under that label this tick.
    ///
    /// Within, never pooled across (`01-SIMULATION.md` §10.4). Pooling tier shares over categories
    /// reverses their sign when exclusion moves units out of the denominator, so the widest
    /// denominator this model will report a share over is one category.
    /// </summary>
    public static double LabelMixShare(Market market, GoodsTable goods, int category, int tier)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(goods);

        var label = goods.LabelOf(category);
        var sold = 0;
        var thisTier = 0;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            if (goods.LabelOf(c) != label)
            {
                continue;
            }

            thisTier += market.Sold(c, tier);

            for (var t = 0; t < goods.TierCount; t++)
            {
                sold += market.Sold(c, t);
            }
        }

        return sold == 0 ? 0.0 : thisTier / (double)sold;
    }

    /// <summary>A basket with nothing in it has an index of 1: it has not moved, because there is nothing to move.</summary>
    private static double Ratio(long now, long opening) => opening == 0 ? 1.0 : now / (double)opening;
}
