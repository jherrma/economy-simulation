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
    private readonly double[] categoryTaste;
    private RandomStream[]? failure;
    private readonly double[] tierValue;
    private readonly double[] archetypeLife;
    private readonly double[] archetypeHazard;

    private Households(int count, int categoryCount, int tierCount, IReadOnlyList<string> archetypeNames)
    {
        Count = count;
        CategoryCount = categoryCount;
        TierCount = tierCount;
        ArchetypeNames = archetypeNames;

        Income = new Money[count];
        TasteWeight = new double[count];
        Theta = new double[count];
        IsAbstainer = new bool[count];
        Archetype = new int[count];
        Age = new int[count * categoryCount];
        Holds = new bool[count * categoryCount];
        Wanted = new bool[count * categoryCount];
        Wait = new int[count * categoryCount];
        categoryTaste = new double[count * categoryCount];

        // Per archetype rather than per household: `value_mult^kappa` is a property of the type, and
        // Math.Pow in the walk's inner loop would be ten million calls a run for an answer that
        // takes four rows of a table.
        tierValue = new double[archetypeNames.Count * categoryCount * tierCount];

        // The same reasoning, and the hazard is kept alongside the life rather than divided out of
        // it in the tick: the failure draw is one comparison per household per good per tick, and a
        // division there is ninety thousand a tick for a number that is fixed for the run.
        archetypeLife = new double[archetypeNames.Count * categoryCount];
        archetypeHazard = new double[archetypeNames.Count * categoryCount];
    }

    public int Count { get; }

    public int CategoryCount { get; }

    public int TierCount { get; }

    /// <summary>The archetype table's types, in the order the loader put them — for reporting.</summary>
    public IReadOnlyList<string> ArchetypeNames { get; }

    /// <summary>Fixed nominal income. There are no wages and no firms, so nothing can change it.</summary>
    public Money[] Income { get; }

    /// <summary>`w_h`, the taste multiplier. Mean exactly 1.</summary>
    public double[] TasteWeight { get; }

    /// <summary>The propensity to reach for credit. Zero for abstainers, in every scenario.</summary>
    public double[] Theta { get; }

    /// <summary>The measured cohort: households that never borrow. What happens to them is the finding.</summary>
    public bool[] IsAbstainer { get; }

    /// <summary>
    /// Which archetype each household is, as an index into <see cref="ArchetypeNames"/>.
    ///
    /// Drawn from its own stream in every run, including under the identity table where the answer
    /// is always zero. See <see cref="Purpose.Archetype"/> for why that draw is not skipped.
    /// </summary>
    public int[] Archetype { get; }

    /// <summary>
    /// `w_h · ŵ_g,A(h) · ε_h,g` — the household's taste for one category, all three levels
    /// multiplied together (`01-SIMULATION.md` §5.4).
    ///
    /// The shared level `w_h` says how keen this household is in general; the archetype's `ŵ` says
    /// what it is keen *about*; `ε` is the residual that stops every household of a type being the
    /// same household. All three are fixed for the life of the run, so the product is computed once.
    ///
    /// `ŵ` is <see cref="Configuration.Archetype.TasteWeightFor"/> — `m / d`, derived, and not the
    /// number §3.5's table states.
    /// </summary>
    public double Taste(int household, int category) => categoryTaste[AgeIndex(household, category)];

    /// <summary>
    /// What this household thinks a tier is worth: `value_mult(tier)^κ_g,A(h)`.
    ///
    /// The exponent goes on the **value** multiplier and never on a price — a price is money and
    /// money is integer cents. `value_mult(standard)` is 1, and 1 to any power is 1, so the median
    /// household's standard-tier valuation is invariant to `κ`: the exponent rotates the ladder
    /// around the standard tier rather than tilting the whole category.
    /// </summary>
    public double ValueMult(int household, int category, int tier) =>
        tierValue[(((Archetype[household] * CategoryCount) + category) * TierCount) + tier];

    /// <summary>
    /// `life_h,g = life_g · d[A(h)][g]` — how long this household's unit of the good lasts, in
    /// ticks (§3.7). A real number: a hazard takes any positive life, so 20.27 months needs no
    /// integer to land on.
    ///
    /// Read by two places and they are the two halves of the composition: the ladder divides the
    /// cash increment by it, and the failure draw compares against its reciprocal.
    /// </summary>
    public double Life(int household, int category) =>
        archetypeLife[(Archetype[household] * CategoryCount) + category];

    /// <summary>
    /// Replacement demand per tick for a good, in units, from the population that was actually
    /// drawn: `Σ_h 1 / life_h,g`.
    ///
    /// This is what the shelf has to supply once the run has settled, and `capacity_g` is
    /// `round(households / life_g)` — the two agree only in expectation. See
    /// <see cref="Simulation.ReplacementResidue"/> for what is done about the gap, which is
    /// nothing.
    /// </summary>
    public double ReplacementDemand(int category)
    {
        var total = 0.0;

        for (var h = 0; h < Count; h++)
        {
            total += archetypeHazard[(Archetype[h] * CategoryCount) + category];
        }

        return total;
    }

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

    /// <summary>
    /// Ticks since the household's unit of a category was bought, category-major per household.
    ///
    /// Under `hazard` this is **kept for reporting and decides nothing** — the geometric
    /// distribution is memoryless, so how long a unit has lasted says nothing about whether it is
    /// about to fail, and reading it would be the calendar creeping back in.
    /// </summary>
    public int[] Age { get; }

    /// <summary>
    /// Whether the household owns a **working** unit of the category.
    ///
    /// **Only `hazard` maintains it.** Under `deterministic` the same fact is `Age[i] < life`, and
    /// this array is written by <see cref="Acquire"/> and read by nobody — deliberately not kept in
    /// step, because the two rules disagree about a life-1 good (the calendar wants one every tick
    /// whether or not it just bought one) and a parallel state that is right most of the time is
    /// worse than one that is plainly unused.
    /// </summary>
    public bool[] Holds { get; }

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
    /// The same step under `hazard`: wanted when no working unit is held.
    ///
    /// The life-1 special case is gone rather than moved. `p = 1 / life` is 1 at `life = 1`, so a
    /// consumable is consumed every tick by the rule that fails a washing machine, and
    /// `life == 1 || age ≥ life` collapses into one condition on one flag.
    /// </summary>
    public void RefreshWantFromHolding(int household, int category)
    {
        var i = AgeIndex(household, category);
        var wants = !Holds[i];

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
        Holds[i] = true;
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
    /// Step 5 under `hazard`: every household draws for every good, and a unit it holds fails when
    /// the draw is below `1 / life_h,g` — **its own** life, which is the good's times its type's
    /// cycle multiplier (§3.7).
    ///
    /// **Every household, every good, every tick, owned or not.** A household with nothing cannot
    /// lose anything, so drawing for it looks like waste — and skipping it makes the stream
    /// position depend on who was rationed, which differs between arms by construction, since being
    /// rationed is the thing under measurement. See <see cref="Purpose.Failure"/>.
    ///
    /// The age is still advanced, and still decides nothing: the hazard is memoryless, so an old
    /// unit is exactly as likely to fail as a new one. It is written to output because a reader
    /// wants to know how long things lasted.
    /// </summary>
    public void FailDurables(GoodsTable goods)
    {
        ArgumentNullException.ThrowIfNull(goods);

        if (failure is null)
        {
            throw new InvalidOperationException(
                "The failure streams were never opened; this population was not drawn for a hazard run.");
        }

        for (var h = 0; h < Count; h++)
        {
            var stream = failure[h];

            for (var c = 0; c < CategoryCount; c++)
            {
                // Drawn first and unconditionally, before anything is asked about what is held.
                var drawn = stream.NextDouble();
                var i = AgeIndex(h, c);

                Age[i]++;

                if (Holds[i] && drawn < archetypeHazard[(Archetype[h] * CategoryCount) + c])
                {
                    Holds[i] = false;
                }
            }
        }
    }

    /// <summary>
    /// Opens one failure stream per household, once, at setup.
    ///
    /// Per household rather than per tick, so a household's failure months are a property of that
    /// household and of the seed — not of how many other households there are. And opened here
    /// rather than in the tick, because <see cref="RandomStream"/> is a class and the tick
    /// allocates nothing.
    /// </summary>
    private void OpenFailureStreams(int runSeed)
    {
        failure = new RandomStream[Count];

        for (var h = 0; h < Count; h++)
        {
            failure[h] = RandomStream.ForHousehold(runSeed, h, Purpose.Failure);
        }
    }

    /// <summary>
    /// A population stated outright rather than drawn: given incomes and taste weights, no
    /// abstainers, one θ for all (0 unless given), every durable at age 0. For tests that need a
    /// household on exactly €650 with `w = 1`, which no seed will ever produce.
    /// </summary>
    internal static Households Specified(
        GoodsTable goods,
        Money[] incomes,
        double[] tasteWeights,
        double theta = 0.0,
        int runSeed = 0)
    {
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(incomes);
        ArgumentNullException.ThrowIfNull(tasteWeights);

        if (incomes.Length != tasteWeights.Length)
        {
            throw new ArgumentException("One taste weight per income.", nameof(tasteWeights));
        }

        var households = new Households(
            incomes.Length,
            goods.CategoryCount,
            goods.TierCount,
            [Configuration.Archetype.AverageName]);

        incomes.CopyTo(households.Income, 0);
        tasteWeights.CopyTo(households.TasteWeight, 0);
        Array.Fill(households.Theta, theta);

        // Every durable working, and the failure streams open: a stated population is usable under
        // either replacement rule, so a test does not have to know which one it is exercising.
        Array.Fill(households.Holds, true);
        households.OpenFailureStreams(runSeed);

        // The identity type: taste is the household's own weight and nothing else, and a tier is
        // worth what the tier table says it is worth.
        for (var h = 0; h < households.Count; h++)
        {
            for (var c = 0; c < households.CategoryCount; c++)
            {
                households.categoryTaste[households.AgeIndex(h, c)] = tasteWeights[h];
            }
        }

        households.FillTierValues(
            goods,
            [
                Configuration.Archetype.Identity(
                    goods.Categories.Select(g => g.Label),
                    goods.Categories.Select(g => g.Name)),
            ]);

        return households;
    }

    /// <summary>
    /// `value_mult(tier)^kappa` and `life_g · d` for every (archetype, category), computed once at
    /// setup.
    /// </summary>
    private void FillTierValues(GoodsTable goods, IReadOnlyList<Configuration.Archetype> types)
    {
        for (var a = 0; a < types.Count; a++)
        {
            for (var c = 0; c < CategoryCount; c++)
            {
                // The exponent by label, the cycle by row: taste is a fact about a category, a
                // replacement cycle a fact about a product (§3.5, §3.7). Under §3.1 the two strings
                // are the same and the distinction is invisible.
                var kappa = types[a].ExponentFor(goods.Categories[c].Label);

                // `d` is exactly 1.0 under the identity table and wherever no type states one, so
                // this is the good's own life to the bit and V5a stays a byte-for-byte comparison.
                var life = goods.Categories[c].Life * types[a].CycleFor(goods.Categories[c].Name);

                archetypeLife[(a * CategoryCount) + c] = life;
                archetypeHazard[(a * CategoryCount) + c] = 1.0 / life;

                for (var t = 0; t < TierCount; t++)
                {
                    // Math.Pow(x, 1.0) is exactly x, so the identity table leaves every multiplier
                    // where it was — which is what V5a rests on.
                    tierValue[(((a * CategoryCount) + c) * TierCount) + t] =
                        Math.Pow(goods.Tiers[t].ValueMult, kappa);
                }
            }
        }
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

        var types = parameters.Archetypes.Types;

        var households = new Households(
            parameters.Run.Households,
            goods.CategoryCount,
            goods.TierCount,
            [.. types.Select(a => a.Name)]);

        households.FillTierValues(goods, types);

        var hazard = parameters.Run.Replacement == Replacement.Hazard;

        if (hazard)
        {
            households.OpenFailureStreams(runSeed);
        }

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

            // Which type this household is, from its own stream and by share. Drawn in every run,
            // including under the identity table where the answer is always zero — see
            // Purpose.Archetype. Its own stream is also what keeps it independent of the abstainer
            // draw: if abstainers were systematically more prudent, the headline would confound
            // "does not borrow" with "wants less" and no invariant here would notice.
            var archetype = Assign(types, RandomStream.ForHousehold(runSeed, h, Purpose.Archetype).NextDouble());

            households.Archetype[h] = archetype;

            // The three levels of taste, multiplied out (§5.4). At sigma_idio = 0 the residual is
            // exactly 1 — Math.Exp(0) — and the draw still happens, so the stream is the same
            // length whatever the setting.
            var residual = RandomStream.ForHousehold(runSeed, h, Purpose.TasteIdiosyncratic);

            for (var c = 0; c < goods.CategoryCount; c++)
            {
                var epsilon = residual.NextLogNormal(1.0, parameters.Archetypes.SigmaIdio);

                // `ŵ = m / d`, derived rather than authored (§3.7). Under E10's tables every `d` is
                // 1 and this is the score multiplier read plainly.
                households.categoryTaste[households.AgeIndex(h, c)] =
                    households.TasteWeight[h]
                    * types[archetype].TasteWeightFor(goods.Categories[c].Label, goods.Categories[c].Name)
                    * epsilon;
            }

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
            if (hazard)
            {
                // Nothing to initialise and nothing to draw. The geometric distribution is
                // memoryless, so there is no age to spread: every household opens owning a working
                // unit of every durable, a fraction `1/life` of them fails in tick 1, and that is
                // already the steady state. `"initial_age"` exists only to separate the first
                // cohort, and under a hazard there is no cohort to separate.
                for (var c = 0; c < goods.CategoryCount; c++)
                {
                    households.Holds[households.AgeIndex(h, c)] = true;
                }

                continue;
            }

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

    /// <summary>
    /// Which type a draw in [0, 1) lands in, walking the shares in table order.
    ///
    /// The table is ordered by name at load, so the assignment depends on the population and not on
    /// the order somebody typed the sections in. The last type absorbs the floating-point tail: the
    /// shares sum to 1 to within 1e-9, which is not the same as summing to 1.
    /// </summary>
    private static int Assign(IReadOnlyList<Configuration.Archetype> types, double draw)
    {
        var cumulative = 0.0;

        for (var a = 0; a < types.Count - 1; a++)
        {
            cumulative += types[a].Share;

            if (draw < cumulative)
            {
                return a;
            }
        }

        return types.Count - 1;
    }
}
