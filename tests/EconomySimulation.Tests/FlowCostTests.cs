using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/04-02. The denominator is a per-tick flow, and financing multiplies it.</summary>
public sealed class FlowCostTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static readonly GoodsTable Goods = new(Defaults);

    private static readonly Market Market = new(Goods);

    private const int Food = 0;
    private const int Electronics = 4;

    // ---- the form -------------------------------------------------------------------------

    /// <summary>`flow_cost = price / life` — the standard basket comes to the mean income, per tick.</summary>
    [Fact]
    public void FlowCostIsPriceOverLife_AndTheStandardBasketComesToTheMeanIncome()
    {
        var total = Flow.Zero;

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var category = Goods.Categories[c];
            var cost = Valuation.FlowCost(Market.Price(c, tier: 1), category.Life);

            Assert.Equal(category.PriceRef.Cents / 100.0 / category.Life, cost.EurosPerTick);
            total += cost;
        }

        Assert.Equal(650.0, total.EurosPerTick, precision: 9);
    }

    /// <summary>
    /// A phone and a month of food, compared as flows: neither is favoured by the form of the
    /// expression. Divide by the *purchase price* instead and the phone looks 36 times worse than
    /// it is — which, since every financeable good is a durable, would land the bias on the
    /// primary result.
    /// </summary>
    [Fact]
    public void APhoneAndAMonthOfFood_AreBothFlows()
    {
        var food = Goods.Categories[Food];
        var phone = Goods.Categories[Electronics];

        var foodCost = Valuation.FlowCost(food.PriceRef, food.Life);
        var phoneCost = Valuation.FlowCost(phone.PriceRef, phone.Life);

        // Per tick, a €900 phone over three years costs less than a month of food.
        Assert.Equal(300.0, foodCost.EurosPerTick);
        Assert.Equal(25.0, phoneCost.EurosPerTick);

        // And the value side is also per tick, so the ratio is comparable: at the median both
        // base scores sit in the same band (1.343 and 1.248, from 02-PARAMETERS.md §3.4).
        var population = Households.Specified(Goods, [Money.FromEuros(650)], [1.0]);
        var foodScore = Valuation.Score(Valuation.BaseValue(Goods, population, 0, Food), foodCost);
        var phoneScore = Valuation.Score(Valuation.BaseValue(Goods, population, 0, Electronics), phoneCost);

        Assert.Equal(1.343, foodScore, precision: 3);
        Assert.Equal(1.248, phoneScore, precision: 3);

        // Against the purchase price the phone's score would be 1.248 / 36 = 0.035. That is the bias.
        var totalPriceScore = Valuation.Score(
            Valuation.BaseValue(Goods, population, 0, Electronics),
            Valuation.FlowCost(phone.PriceRef, life: 1));
        Assert.True(totalPriceScore < 0.04);
    }

    // ---- financing ------------------------------------------------------------------------

    /// <summary>`finance_mult` comes from `Rate`: 1.08 over twelve months, 1.16 over twenty-four, exactly.</summary>
    [Fact]
    public void TheFinanceMultiplierComesFromRate()
    {
        var cash = new Flow(100.0);

        // The multipliers are bit-exact (RateTests); their product with an amount is the nearest
        // double, which for 116 is one ulp short.
        Assert.Equal(108.0, Valuation.FinancedCost(cash, Defaults.Credit.LoanRate, 12).EurosPerTick, precision: 12);
        Assert.Equal(116.0, Valuation.FinancedCost(cash, Defaults.Credit.LoanRate, 24).EurosPerTick, precision: 12);
        Assert.Equal(100.0, Valuation.FinancedCost(cash, Rate.Zero, 24).EurosPerTick);
    }

    /// <summary>
    /// Financing strictly worsens a candidate's score, for every category, at every tier, at
    /// every income in the specification's ladder. This is the assertion that keeps the model
    /// honest: if a bug ever makes financing attractive on price, the number that comes out of
    /// the experiment is a tautology.
    /// </summary>
    [Fact]
    public void FinancingStrictlyWorsensEveryScore()
    {
        int[] incomes = [300, 450, 650, 900, 1500, 3000];
        var rate = Defaults.Credit.LoanRate;

        foreach (var income in incomes)
        {
            var population = Households.Specified(Goods, [Money.FromEuros(income)], [1.0]);

            for (var c = 0; c < Goods.CategoryCount; c++)
            {
                var category = Goods.Categories[c];
                var term = category.Financeable ? category.Term : 24;

                for (var t = 0; t < Goods.TierCount; t++)
                {
                    var value = Valuation.FlowValue(Goods, population, 0, c, t);
                    var cashCost = Valuation.FlowCost(Market.Price(c, t), category.Life);
                    var financedCost = Valuation.FinancedCost(cashCost, rate, term);

                    var cash = Valuation.Score(value, cashCost);
                    var financed = Valuation.Score(value, financedCost);

                    Assert.True(
                        financed < cash,
                        $"{category.Name} tier {t} at €{income}: financed {financed:0.000} is not below cash {cash:0.000}");

                    // And by exactly the multiplier, so nothing else has crept into the ratio.
                    Assert.Equal(cash / rate.FinanceMultiplier(term), financed, precision: 12);
                }
            }
        }
    }

    /// <summary>
    /// The multiplier is a multiplier: it does not change the ordering of candidates, only their
    /// height. A household that finances everything faces the same ladder, lowered.
    /// </summary>
    [Fact]
    public void FinancingLowersScoresWithoutReorderingThem()
    {
        var population = Households.Specified(Goods, [Money.FromEuros(650)], [1.0]);
        var rate = Defaults.Credit.LoanRate;

        var cash = new double[Goods.ShelfCount];
        var financed = new double[Goods.ShelfCount];

        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            for (var t = 0; t < Goods.TierCount; t++)
            {
                var value = Valuation.FlowValue(Goods, population, 0, c, t);
                var cost = Valuation.FlowCost(Market.Price(c, t), Goods.Categories[c].Life);

                cash[Goods.Index(c, t)] = Valuation.Score(value, cost);
                financed[Goods.Index(c, t)] = Valuation.Score(value, Valuation.FinancedCost(cost, rate, 24));
            }
        }

        var cashOrder = Enumerable.Range(0, Goods.ShelfCount).OrderByDescending(i => cash[i]).ToArray();
        var financedOrder = Enumerable.Range(0, Goods.ShelfCount).OrderByDescending(i => financed[i]).ToArray();

        Assert.Equal(cashOrder, financedOrder);
    }

    // ---- life = 1 -------------------------------------------------------------------------

    /// <summary>A category with `life = 1` is not financeable, and its flow cost is its price.</summary>
    [Fact]
    public void ALifeOfOne_IsNotFinanceable_AndCostsItsPrice()
    {
        for (var c = 0; c < Goods.CategoryCount; c++)
        {
            var category = Goods.Categories[c];

            if (category.Life != 1)
            {
                continue;
            }

            Assert.False(category.Financeable, $"{category.Name} has life 1 and is financeable");
            Assert.Equal(0, category.Term);

            for (var t = 0; t < Goods.TierCount; t++)
            {
                Assert.Equal(
                    Market.Price(c, t).Cents / 100.0,
                    Valuation.FlowCost(Market.Price(c, t), category.Life).EurosPerTick);
            }
        }

        Assert.Contains(Goods.Categories, c => c.Life == 1);
    }

    // ---- allocation -----------------------------------------------------------------------

    [Fact]
    public void CostingEveryShelf_AllocatesNothing()
    {
        var total = Flow.Zero;

        void CostEveryShelf()
        {
            for (var i = 0; i < 1000; i++)
            {
                for (var c = 0; c < Goods.CategoryCount; c++)
                {
                    for (var t = 0; t < Goods.TierCount; t++)
                    {
                        var cost = Valuation.FlowCost(Market.Price(c, t), Goods.Categories[c].Life);
                        total += Valuation.FinancedCost(cost, Defaults.Credit.LoanRate, 24);
                    }
                }
            }
        }

        // Warmed through Allocations, and with the **whole loop** as the warm-up rather than one
        // call of each method. A single call promotes nothing: tiered compilation needs about
        // thirty, and a loop this long also draws on-stack replacement into the measured window.
        // Either landing there is several kilobytes of runtime that belong to no model code.
        var allocated = Infrastructure.Allocations.Of(CostEveryShelf, CostEveryShelf);

        Assert.True(total.IsPositive);
        Assert.Equal(0, allocated);
    }
}
