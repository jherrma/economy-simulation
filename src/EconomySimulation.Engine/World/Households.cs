using EconomySimulation.Engine.Configuration;

namespace EconomySimulation.Engine.World;

/// <summary>
/// The population, drawn once at initialisation from named streams.
///
/// A household is an index into parallel arrays, not an object. That is partly for the walk, which
/// must not allocate, and partly because every attribute is then something that can be added later
/// without disturbing the ones already there — a new array and a new stream, and every existing
/// draw is exactly where it was.
///
/// What a household *is* lives here; what it *holds* lives in the ledger. Cash is a balance, and
/// balances have exactly one home.
/// </summary>
public sealed class Households
{
    private Households(int count, int categoryCount)
    {
        Count = count;
        CategoryCount = categoryCount;

        Income = new Money[count];
        TasteWeight = new double[count];
        Theta = new double[count];
        IsAbstainer = new bool[count];
        Age = new int[count * categoryCount];
    }

    public int Count { get; }

    public int CategoryCount { get; }

    /// <summary>Fixed nominal income. There are no wages and no firms, so nothing can change it.</summary>
    public Money[] Income { get; }

    /// <summary>`w_h`, the taste multiplier. Mean exactly 1.</summary>
    public double[] TasteWeight { get; }

    /// <summary>The propensity to reach for credit. Zero for abstainers, in every scenario.</summary>
    public double[] Theta { get; }

    /// <summary>The measured cohort: households that never borrow. What happens to them is the finding.</summary>
    public bool[] IsAbstainer { get; }

    /// <summary>Opening cash: `income_h · opening_cash_share`, handed to the ledger to hold.</summary>
    public Money[] OpeningCash(double openingCashShare)
    {
        var cash = new Money[Count];

        for (var h = 0; h < Count; h++)
        {
            cash[h] = Income[h].Scaled(openingCashShare);
        }

        return cash;
    }

    /// <summary>Ticks since the household's unit of a category was bought, category-major per household.</summary>
    public int[] Age { get; }

    public int AgeIndex(int household, int category) => (household * CategoryCount) + category;

    /// <summary>
    /// Draws a population for one seed.
    ///
    /// Every attribute comes from its own named stream, so the abstainer set and the income
    /// distribution are the same households in every scenario for a given seed. The finding is a
    /// difference between two runs for the same fifth of households; if the membership moved, the
    /// difference would include a composition change nobody could decompose.
    /// </summary>
    public static Households Draw(SimulationParameters parameters, GoodsTable goods, int runSeed)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(goods);

        var households = new Households(parameters.Run.Households, goods.CategoryCount);
        var meanIncome = parameters.Income.MeanIncome;

        for (var h = 0; h < households.Count; h++)
        {
            var income = RandomStream
                .ForHousehold(runSeed, h, Purpose.Income)
                .NextLogNormal(1.0, parameters.Income.SigmaIncome);

            households.Income[h] = meanIncome.Scaled(income);

            households.TasteWeight[h] = RandomStream
                .ForHousehold(runSeed, h, Purpose.Willingness)
                .NextLogNormal(1.0, parameters.Decision.SigmaW);

            // Drawn per household rather than by assigning an exact count, so that adding a
            // household disturbs nobody else's draw. Cohort size then varies a little by seed,
            // which costs nothing: the comparison is paired within a seed, so it cancels exactly.
            households.IsAbstainer[h] = RandomStream
                .ForHousehold(runSeed, h, Purpose.Abstainer)
                .NextDouble() < parameters.Run.AbstainerShare;

            // θ is drawn in every scenario, including the baseline where it is never read. The
            // draw has to happen in the same place in the same stream in both, or credit_off and
            // credit_high sit on different random worlds and the paired comparison is worthless.
            // It costs one draw per household.
            var theta = RandomStream.ForHousehold(runSeed, h, Purpose.Theta).NextDouble();

            households.Theta[h] = households.IsAbstainer[h]
                ? 0.0
                : parameters.Credit.ThetaMin
                  + ((parameters.Credit.ThetaMax - parameters.Credit.ThetaMin) * theta);

            // Uniform over the good's life, and its absence is spectacular: initialise every
            // durable at zero and the whole town replaces its appliances in the same month for
            // the life of the run. The output is a clean sawtooth with period `life`, it looks
            // exactly like a business cycle, and nothing in this model should produce one.
            var ages = RandomStream.ForHousehold(runSeed, h, Purpose.InitialAge);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                households.Age[households.AgeIndex(h, c)] = ages.NextInt(goods.Categories[c].Life);
            }
        }

        return households;
    }
}
