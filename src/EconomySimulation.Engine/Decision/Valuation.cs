using EconomySimulation.Engine.World;

namespace EconomySimulation.Engine.Decision;

/// <summary>
/// The two sides of the decision, as flows (`01-SIMULATION.md` §5).
///
/// <code>
/// flow_value(h, g, t) = (a_g + b_g · income_h) · w_h · ŵ_g,A(h) · ε_h,g · value_mult_t ^ κ_g,A(h)
/// flow_cost(g, t)     = price_(g,t) / life_h,g
/// </code>
///
/// The three taste factors are multiplied out once, at initialisation, and read back as
/// <see cref="Households.Taste"/>; the exponent is folded into
/// <see cref="Households.ValueMult"/> the same way. Both are fixed for the life of the run.
/// The exponent is applied to the tier's **value** multiplier and never to a price.
///
/// `a_g` is the Stone-Geary floor: the part of a good's worth that does not scale with income. It
/// is not a refinement. With value strictly proportional to income every good is a luxury, a
/// household on €450 scores food at 0.93 and buys none, and no invariant in the project notices
/// that the poor end of the distribution has stopped eating. The split is neutral at the mean
/// income by construction, so it changes the income gradient of demand and nothing else.
///
/// The tier's value multiplier is applied here, at the point of use, and is never folded into
/// `v_g`. Eighteen precomputed value weights would be faster to look up and would make the
/// diminishing-returns property of the upgrade ladder impossible to state, because that property
/// is about the relationship between the multipliers.
/// </summary>
public static class Valuation
{
    /// <summary>`V = (a_g + b_g · income_h) · w_h`, before any tier multiplier.</summary>
    public static Flow BaseValue(Money floor, double incomeSlope, Money incomeOfHousehold, double tasteWeight)
    {
        // Cents throughout, then euros once. The floor is exact; the income-linked part is a
        // product of a pure number and an integer, and the whole thing scales exactly when every
        // nominal quantity is doubled — which is what nominal neutrality (V3) rests on.
        var cents = floor.Cents + (incomeSlope * incomeOfHousehold.Cents);

        return new Flow(cents / 100.0) * tasteWeight;
    }

    /// <summary>The same, read off the world.</summary>
    public static Flow BaseValue(GoodsTable goods, Households population, int household, int category)
    {
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(population);

        return BaseValue(
            goods.Floor(category),
            goods.IncomeSlope(category),
            population.Income[household],
            population.Taste(household, category));
    }

    /// <summary>`flow_value` at a tier: the base value times that tier's value multiplier.</summary>
    public static Flow FlowValue(GoodsTable goods, Households population, int household, int category, int tier)
    {
        ArgumentNullException.ThrowIfNull(goods);

        return BaseValue(goods, population, household, category)
               * population.ValueMult(household, category, tier);
    }

    /// <summary>`flow_cost = price / life`, per tick. Never the purchase price.</summary>
    public static Flow FlowCost(Money price, double life) => Flow.Spread(price, life);

    /// <summary>
    /// What a financed candidate costs per tick: the cash cost times `finance_mult`, which comes
    /// from <see cref="Rate"/> — the one place in the engine that turns a rate and a term into a
    /// multiplier, and the only file allowed to know the divisor.
    ///
    /// Since the multiplier exceeds 1 whenever the rate and the term do, **financing strictly
    /// worsens a candidate's score**. Credit never makes anything look cheaper in this model; what
    /// it does is put within reach a tier that cash could not pay for. If a change ever makes this
    /// method return less than its input, the hypothesis is being assumed rather than tested, and
    /// FlowCostTests says so for every category and every tier.
    /// </summary>
    public static Flow FinancedCost(Flow cashCost, Rate loanRate, int termMonths) =>
        cashCost * loanRate.FinanceMultiplier(termMonths);

    /// <summary>
    /// `Δvalue / Δcost`, a pure number, compared against λ.
    ///
    /// A non-positive cost with a positive value is a step that is better and no dearer — it can
    /// arise once tiers have repriced independently and a higher tier has fallen to the price of
    /// the one below it. It scores as infinitely good, which is what it is. A non-positive value
    /// scores zero: nothing worthless clears λ whatever it costs.
    /// </summary>
    public static double Score(Flow deltaValue, Flow deltaCost)
    {
        if (!deltaValue.IsPositive)
        {
            return 0.0;
        }

        return deltaCost.IsPositive ? deltaValue / deltaCost : double.PositiveInfinity;
    }
}
