namespace EconomySimulation.Engine.Decision;

/// <summary>
/// One step a household can take in one category: buy the budget unit, or move the unit it has
/// already decided on up one tier. Never "a tier".
///
/// Ranking the three *tiers* by total score is the obvious implementation and it is wrong in a way
/// that produces a working run: `value_mult / price_mult` is 1.133 for budget against 1.000 for
/// standard and 0.778 for premium, so every household buys budget everything and premium never
/// sells at any price. Only the increment answers the question the household is actually asking —
/// is the next step up worth what it costs? — so the increment is what is scored.
/// </summary>
/// <param name="Category">The category the step is in.</param>
/// <param name="Tier">The tier the step lands on. Tier 0 is a purchase; anything above is an upgrade.</param>
/// <param name="DeltaValue">What the step adds, in euros per tick: `V · (value_mult_t − value_mult_(t−1))`.</param>
/// <param name="DeltaPrice">What the step costs in cash, now: `price_t − price_(t−1)`. The increments telescope to the tier's posted price exactly.</param>
/// <param name="Life">The category's life, which turns the cash increment into a per-tick cost.</param>
/// <param name="Score">`Δvalue / Δcost`, a pure number, compared against λ.</param>
public readonly record struct Candidate(
    int Category,
    int Tier,
    Flow DeltaValue,
    Money DeltaPrice,
    int Life,
    double Score)
{
    /// <summary>An upgrade needs the step below it taken first; a purchase needs nothing.</summary>
    public bool IsUpgrade => Tier > 0;

    /// <summary>The per-tick cost of the step: the cash increment spread over the good's life.</summary>
    public Flow DeltaCost => Flow.Spread(DeltaPrice, Life);
}
