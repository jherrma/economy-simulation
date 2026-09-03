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
        Wanted = new bool[count * categoryCount];
        Wait = new int[count * categoryCount];
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

    /// <summary>
    /// Whether the household wants a unit of the category this tick. A bool, not a count: the
    /// model cannot represent buying *more*, only buying *better*, which is why the tier ladder
    /// exists. Extra income goes into quality, never into quantity.
    /// </summary>
    public bool[] Wanted { get; }

    /// <summary>
    /// Ticks since the open want first appeared: 0 the tick it appears, +1 every tick it goes
    /// unmet, back to 0 on purchase. A household priced out of a phone this tick is not out of the
    /// market; it is still in it next tick, and how long it stays there is the cleanest thing the
    /// model can say about the timing channel (07-03).
    /// </summary>
    public int[] Wait { get; }

    /// <summary>Where (household, category) lives in <see cref="Age"/>, <see cref="Wanted"/> and <see cref="Wait"/>.</summary>
    public int AgeIndex(int household, int category) => (household * CategoryCount) + category;

    /// <summary>
    /// Step 3 for one household and one category: wanted if `life = 1`, or if the unit held has
    /// reached the end of its life. An unmet want persists — the unit keeps ageing past its life,
    /// so the condition stays true until a purchase resets it — and `wait` counts the ticks.
    /// </summary>
    public void RefreshWant(int household, int category, int life)
    {
        var i = AgeIndex(household, category);
        var wants = life == 1 || Age[i] >= life;

        Wait[i] = wants && Wanted[i] ? Wait[i] + 1 : 0;
        Wanted[i] = wants;
    }

    /// <summary>
    /// The household now holds a fresh unit of the category: age 0, want met, wait over. The one
    /// operation the walk performs on a household's holdings.
    /// </summary>
    public void Acquire(int household, int category)
    {
        var i = AgeIndex(household, category);

        Age[i] = 0;
        Wanted[i] = false;
        Wait[i] = 0;
    }

    /// <summary>
    /// Step 5: every held durable gets a tick older. Runs after the walk, so a unit bought this
    /// tick starts at 0 and is not wanted again next tick. Non-durables have no age.
    /// </summary>
    public void AgeDurables(GoodsTable goods)
    {
        ArgumentNullException.ThrowIfNull(goods);

        for (var c = 0; c < CategoryCount; c++)
        {
            if (!goods.IsDurable(c))
            {
                continue;
            }

            for (var h = 0; h < Count; h++)
            {
                Age[AgeIndex(h, c)]++;
            }
        }
    }

    /// <summary>
    /// A population stated outright rather than drawn: given incomes and taste weights, no
    /// abstainers, one θ for all (0 unless given), every durable at age 0. For tests that need a
    /// household on exactly €650 with `w = 1`, which no seed will ever produce.
    /// </summary>
    internal static Households Specified(int categoryCount, Money[] incomes, double[] tasteWeights, double theta = 0.0)
    {
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(tasteWeights);

        if (incomes.Length != tasteWeights.Length)
        {
            throw new ArgumentException("One taste weight per income.", nameof(tasteWeights));
        }

        var households = new Households(incomes.Length, categoryCount);
        incomes.CopyTo(households.Income, 0);
        tasteWeights.CopyTo(households.TasteWeight, 0);
        Array.Fill(households.Theta, theta);

        return households;
    }

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
            //
            // Over {1 … life}, not {0 … life − 1}: a unit is wanted when age ≥ life and wants are
            // asked before ageing, so an age of `life` is due in tick 1 and an age of 1 in tick
            // `life`. Over {0 … life − 1} nothing at all would be due in tick 1 and the first cohort
            // would land in tick 2 — a one-tick hole at the start of every durable's series. A
            // non-durable has no age and stays at 0; the draw still happens, so the stream is the
            // same length whatever the category's life.
            var ages = RandomStream.ForHousehold(runSeed, h, Purpose.InitialAge);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                var life = goods.Categories[c].Life;
                var drawn = ages.NextInt(life);

                households.Age[households.AgeIndex(h, c)] = life > 1 ? drawn + 1 : 0;
            }
        }

        return households;
    }
}
