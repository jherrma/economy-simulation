using EconomySimulation.Engine.World;

namespace EconomySimulation.Engine.Decision;

/// <summary>
/// The candidates a household faces in one wanted category (`01-SIMULATION.md` §5.1): one per
/// tier, each an increment on the one below.
///
/// At opening prices the ladder is already sorted — `value_mult` rises more slowly than
/// `price_mult`, so each upgrade scores lower than the step under it and diminishing returns to
/// quality fall out of the parameters rather than being imposed. Once tiers have repriced
/// independently that need not hold: budget rising 14% against standard makes the upgrade a
/// better ratio than the purchase. The ladder still reports the steps in tier order; ranking them
/// is the walk's business, and the walk does not assume they arrive sorted.
/// </summary>
public static class Ladder
{
    /// <summary>
    /// Fills <paramref name="into"/> with one candidate per tier for the category and returns how
    /// many it wrote. Allocation-free: the caller owns the buffer.
    /// </summary>
    public static int Build(
        GoodsTable goods,
        Market market,
        Households population,
        int household,
        int category,
        Span<Candidate> into)
    {
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(market);

        if (into.Length < goods.TierCount)
        {
            throw new ArgumentException(
                $"A ladder has {goods.TierCount} steps; the buffer holds {into.Length}.",
                nameof(into));
        }

        var baseValue = Valuation.BaseValue(goods, population, household, category);

        // The household's own life, not the good's (§3.7). This is the half of the composition that
        // makes `ŵ = m / d` come out at `m`: a type that replaces a phone half as often pays half
        // as much per tick for it, and its derived taste weight is what puts the score back.
        var life = population.Life(household, category);

        var previousMult = 0.0;
        var previousPrice = Money.Zero;

        for (var t = 0; t < goods.TierCount; t++)
        {
            // The household's own view of the tier: value_mult^kappa (§5.4). Under the identity
            // table kappa is 1 and this is the tier table's number, to the bit.
            var mult = population.ValueMult(household, category, t);
            var price = market.Price(category, t);

            var deltaValue = baseValue * (mult - previousMult);
            var deltaPrice = price - previousPrice;

            into[t] = new Candidate(
                category,
                t,
                deltaValue,
                deltaPrice,
                life,
                Valuation.Score(deltaValue, Flow.Spread(deltaPrice, life)));

            previousMult = mult;
            previousPrice = price;
        }

        return goods.TierCount;
    }

    /// <summary>
    /// The tier a household lands on with unlimited stock and unlimited cash: up the ladder from
    /// the bottom, stopping at the first step that does not clear λ. −1 is "buys nothing". This
    /// is what `02-PARAMETERS.md` §3.4's ladder table describes, and it is the decision rule with
    /// every constraint removed — the walk adds the constraints and nothing else.
    /// </summary>
    public static int UnconstrainedTier(ReadOnlySpan<Candidate> ladder, double lambda)
    {
        var chosen = -1;

        for (var t = 0; t < ladder.Length; t++)
        {
            if (ladder[t].Score < lambda)
            {
                break;
            }

            chosen = t;
        }

        return chosen;
    }
}
