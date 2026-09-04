using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.Output;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Engine.Decision;

/// <summary>What happened to one candidate for one household.</summary>
public enum WalkOutcome
{
    /// <summary>Paid for in cash, and the category's chosen tier moved up a step.</summary>
    Taken,

    /// <summary>Paid for with a loan for the increment, and the chosen tier moved up a step.</summary>
    Financed,

    /// <summary>Willing and able, and the shelf was empty. Counted as demand.</summary>
    Blocked,

    /// <summary>Willing, and could neither pay cash nor finance. Not demand.</summary>
    Unaffordable,

    /// <summary>
    /// Willing, able to finance, and with `money_creation` off the pool could not fund the loan.
    /// Credit rationing: recorded, not halted. Counted as unaffordable against the tier.
    /// </summary>
    Rationed,
}

/// <summary>For tests: sees every outcome as it happens. Null in a run, so the walk stays allocation-free.</summary>
internal delegate void WalkObserver(int household, in Candidate candidate, WalkOutcome outcome);

/// <summary>
/// Step 4 — the shopping walk (`01-SIMULATION.md` §6). About 95% of the tick, and allocation-free
/// from the first commit: every buffer is owned here and sized once.
///
/// Households are visited in a seeded random order redrawn every tick. Each walks its candidates
/// — up to three per wanted category — from the best score down, against a cash budget that
/// depletes as it goes. Two things about the walk are choices rather than mechanics, and both are
/// the conservative choice:
///
/// **The budget is sequential.** Each candidate taken changes what is affordable next, so the
/// tier a household lands on depends on what it bought first. Precomputing affordability for the
/// list would let a household take an upgrade it can no longer pay for.
///
/// **Rationing is first-come within the random order.** Nobody is favoured by wealth or
/// willingness, so any crowding-out the model produces is caused by *ability to bid at all* —
/// the mechanism under test. `rationing = willingness` serves keener households first; it is
/// expected to strengthen the result and must never be the default.
/// </summary>
public sealed class Walker
{
    private readonly SimulationParameters parameters;
    private readonly GoodsTable goods;
    private readonly Market market;
    private readonly Households population;
    private readonly Ledger.Ledger books;
    private readonly LoanBook loans;
    private readonly int runSeed;

    private readonly RandomStream orderStream;
    private readonly int[] order;
    private readonly double[] keenness;
    private readonly Candidate[] candidates;
    private readonly bool[] visited;
    private readonly int[] chosenTier;
    private readonly RandomStream[] financeStreams;

    public Walker(
        SimulationParameters parameters,
        GoodsTable goods,
        Market market,
        Households population,
        Ledger.Ledger books,
        LoanBook loans,
        int runSeed)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(loans);

        this.parameters = parameters;
        this.goods = goods;
        this.market = market;
        this.population = population;
        this.books = books;
        this.loans = loans;
        this.runSeed = runSeed;

        // One finance stream per household, opened once: the θ coin is flipped per candidate
        // reached, so a household's stream advances only when it actually got as far as the
        // finance branch. With credit off it is never touched, which is what V5 needs of it.
        financeStreams = new RandomStream[population.Count];

        for (var h = 0; h < population.Count; h++)
        {
            financeStreams[h] = RandomStream.ForHousehold(runSeed, h, Purpose.Finance);
        }

        orderStream = RandomStream.ForTick(runSeed, 0, Purpose.Order);
        order = new int[population.Count];
        keenness = new double[population.Count];
        candidates = new Candidate[goods.GoodCount];
        visited = new bool[goods.GoodCount];
        chosenTier = new int[goods.CategoryCount];
    }

    /// <summary>Set by tests to watch outcomes. Leave null in a run.</summary>
    internal WalkObserver? Observer { get; set; }

    /// <summary>
    /// Where purchases are reported for the cohort series (07-03). Set by the simulation; null in
    /// tests that drive a walker directly, which then record nothing. The tier and the price paid
    /// are known here and nowhere else — after the walk, only that *something* was bought survives.
    /// </summary>
    internal CohortMetrics? Cohorts { get; set; }

    /// <summary>The order the last tick visited households in, for tests of the shuffle.</summary>
    internal ReadOnlySpan<int> LastOrder => order;

    /// <summary>
    /// Loans the pool could not fund this tick, with `money_creation` off. Credit is then genuinely
    /// scarce, and who gets it is decided by the random household order — the closest v1 comes to
    /// a lending constraint. Always zero with creation on.
    /// </summary>
    public int Rationed { get; private set; }

    public Result Run(int tick)
    {
        Rationed = 0;
        Order(tick);

        for (var i = 0; i < order.Length; i++)
        {
            var result = WalkOne(order[i]);

            if (!Results.IsOk(result))
            {
                return result;
            }
        }

        return Results.Ok;
    }

    // ---- who shops first ----------------------------------------------------------------------

    // CS8524 only: a new rationing rule must break this build, which is CS8509 and stays armed.
#pragma warning disable CS8524
    private void Order(int tick)
    {
        // A seeded shuffle, redrawn every tick from the tick's own stream. Reseeding in place
        // rather than constructing a stream keeps the tick free of allocation.
        orderStream.RestartForTick(runSeed, tick, Purpose.Order);

        for (var h = 0; h < order.Length; h++)
        {
            order[h] = h;
        }

        orderStream.Shuffle(order.AsSpan());

        switch (parameters.Prices.Rationing)
        {
            case Rationing.Random:
                break;

            case Rationing.Willingness:
                // Keener households first; the shuffle just performed breaks ties. Negated so
                // that an ascending sort puts the largest taste weight first.
                for (var i = 0; i < order.Length; i++)
                {
                    // The **shared** level w_h, deliberately: willingness rationing orders whole
                    // households for a tick, and a household is not more or less willing depending
                    // on which category it happens to be standing in front of.
                    keenness[i] = -population.TasteWeight[order[i]];
                }

                Array.Sort(keenness, order);
                break;
        }
    }
#pragma warning restore CS8524

    // ---- one household ------------------------------------------------------------------------

    private Result WalkOne(int household)
    {
        var count = 0;

        for (var c = 0; c < goods.CategoryCount; c++)
        {
            chosenTier[c] = -1;

            if (population.Wanted[population.AgeIndex(household, c)])
            {
                count += Ladder.Build(goods, market, population, household, c, candidates.AsSpan(count));
            }
        }

        Array.Clear(visited, 0, count);

        var lambda = LambdaFor(household);

        while (true)
        {
            // The best candidate that is both unvisited and available. An upgrade becomes
            // available when the step below it is taken, and is then ranked among what remains —
            // the ladder is sorted at opening prices and need not stay so once tiers reprice
            // independently, so the walk does not assume it arrives sorted.
            var best = -1;

            for (var i = 0; i < count; i++)
            {
                if (visited[i] || !Available(candidates[i]))
                {
                    continue;
                }

                if (best < 0 || candidates[i].Score > candidates[best].Score)
                {
                    best = i;
                }
            }

            // Nothing left, or nothing left that clears λ: stop. Nothing further can clear it.
            if (best < 0 || candidates[best].Score < lambda)
            {
                break;
            }

            visited[best] = true;
            ref readonly var candidate = ref candidates[best];

            // Ability before stock. "Blocked" means willing and able with nowhere to go, because
            // that and only that is demand the price should see (05-01); a household that could
            // not have paid would not have bought from a full shelf either. Cash first; the
            // finance branch is reached only when cash failed.
            var finance = false;

            if (candidate.DeltaPrice > books.Cash(household))
            {
                if (!CanFinance(household, in candidate, lambda))
                {
                    market.RecordUnaffordable(candidate.Category, candidate.Tier);
                    Observer?.Invoke(household, in candidate, WalkOutcome.Unaffordable);
                    continue;
                }

                finance = true;
            }

            if (market.Stock(candidate.Category, candidate.Tier) == 0)
            {
                market.RecordBlocked(candidate.Category, candidate.Tier);
                Observer?.Invoke(household, in candidate, WalkOutcome.Blocked);
                continue;
            }

            if (finance)
            {
                var originated = Originate(household, in candidate, out var funded);

                if (!Results.IsOk(originated))
                {
                    return originated;
                }

                if (!funded)
                {
                    // Credit rationing (06-04): the pool could not fund the loan. Recorded, never halted.
                    market.RecordUnaffordable(candidate.Category, candidate.Tier);
                    Rationed++;
                    Observer?.Invoke(household, in candidate, WalkOutcome.Rationed);
                    continue;
                }
            }

            var paid = Pay(household, candidate.DeltaPrice);

            if (!Results.IsOk(paid))
            {
                return paid;
            }

            chosenTier[candidate.Category] = candidate.Tier;
            Observer?.Invoke(household, in candidate, finance ? WalkOutcome.Financed : WalkOutcome.Taken);
        }

        // Stock is consumed once per category, at the final tier, and the unit is new.
        for (var c = 0; c < goods.CategoryCount; c++)
        {
            if (chosenTier[c] >= 0)
            {
                var tier = chosenTier[c];

                market.Sell(c, tier);

                // Before Acquire, which clears the wait this purchase ended.
                Cohorts?.RecordPurchase(
                    CohortMetrics.Of(population, household),
                    c,
                    tier,
                    market.Price(c, tier),
                    population.Wait[population.AgeIndex(household, c)]);

                population.Acquire(household, c);
            }
        }

        return Results.Ok;
    }

    /// <summary>
    /// The household's threshold this tick: `λ · min(1, φ / b_h)`, with `b_h` its cash in months
    /// of its own income after income and debt service. Cash below φ months leaves λ alone; cash
    /// above it lowers λ, so a household that has been banking part of its income starts taking
    /// upgrades it would not otherwise take. Dimensionless throughout, so nominal neutrality holds.
    /// </summary>
    private double LambdaFor(int household)
    {
        var lambda = parameters.Decision.Lambda;
        var phi = parameters.Decision.BufferMonths;
        var income = population.Income[household].Cents;

        if (phi <= 0.0 || income <= 0)
        {
            return lambda;
        }

        var bufferRatio = books.Cash(household).Cents / (double)income; // months of own income

        return bufferRatio > phi ? lambda * (phi / bufferRatio) : lambda;
    }

    // ---- credit -------------------------------------------------------------------------------

    /// <summary>
    /// Whether a household that cannot pay cash for the candidate may finance it. Four conditions
    /// in the spec's order, each reached only if the one before held: the category is financeable
    /// and credit is on; a draw from the household's finance stream is below `θ_h` (abstainers
    /// have `θ = 0` and never pass); the **financed score** — the cash score over `finance_mult` —
    /// still clears the household's λ, so credit never makes anything look cheaper; and the
    /// instalment fits **this tick** against the residual, income less running debt service less
    /// the subsistence share. The residual counts loans originated earlier in this same walk.
    ///
    /// The one-tick horizon is a behaviour under test, not an assumption: a household short of
    /// cash looks at the monthly payment while one with cash looks at the price. `full_term` is the
    /// control, and asks the whole repayable amount to fit the residual instead — the price test a
    /// cash buyer applies, put to the borrower — under which loan stacking all but disappears.
    /// </summary>
    private bool CanFinance(int household, in Candidate candidate, double lambda)
    {
        var category = candidate.Category;

        if (!parameters.Credit.CreditEnabled || !goods.IsFinanceable(category) || !(candidate.DeltaPrice > Money.Zero))
        {
            return false;
        }

        if (!(financeStreams[household].NextDouble() < population.Theta[household]))
        {
            return false;
        }

        var term = goods.Categories[category].Term;
        var rate = parameters.Credit.LoanRate;

        if (candidate.Score / rate.FinanceMultiplier(term) < lambda)
        {
            return false;
        }

        var loan = Loan.Originate(household, category, candidate.DeltaPrice, rate, term);
        var income = population.Income[household];
        var residual = income - loans.DebtService(household) - income.Scaled(parameters.Decision.SubsistenceShare);

        // CS8524 only: a new horizon must break this build, which is CS8509 and stays armed.
#pragma warning disable CS8524
        var burden = parameters.Decision.AffordabilityHorizon switch
        {
            AffordabilityHorizon.Myopic => loan.Instalment(0),
            AffordabilityHorizon.FullTerm => loan.Principal + loan.InterestTotal,
        };
#pragma warning restore CS8524

        return burden <= residual;
    }

    /// <summary>
    /// A loan for the increment, never for the whole tier price. With creation on, new money
    /// appears in the borrower's cash against a claim of the same size; with it off, the principal
    /// comes out of the pool — and if the pool cannot cover it, `funded` is false: rationed,
    /// not failed. The increment is then spent into the pool by the caller like any cash purchase.
    /// </summary>
    private Result Originate(int household, in Candidate candidate, out bool funded)
    {
        var principal = candidate.DeltaPrice;
        var borrower = Account.Household(household);
        funded = false;

        if (parameters.Credit.MoneyCreation)
        {
            var created = books.CreateMoney(borrower, principal, TransferReason.LoanOrigination);

            if (!Results.IsOk(created))
            {
                return created;
            }
        }
        else
        {
            if (books.Pool < principal)
            {
                return Results.Ok;
            }

            var drawn = books.Transfer(Account.Pool, borrower, principal, TransferReason.LoanOrigination);

            if (!Results.IsOk(drawn))
            {
                return drawn;
            }
        }

        books.AddClaim(principal);
        loans.Add(Loan.Originate(household, candidate.Category, principal, parameters.Credit.LoanRate, goods.Categories[candidate.Category].Term));
        funded = true;

        return Results.Ok;
    }

    private bool Available(in Candidate candidate) =>
        !candidate.IsUpgrade || chosenTier[candidate.Category] == candidate.Tier - 1;

    /// <summary>
    /// The increment changes hands. Normally household to pool; a negative increment — a higher
    /// tier that has repriced below the one under it — comes back the other way, so that what the
    /// household has paid in total is always the posted price of the tier it holds.
    /// </summary>
    private Result Pay(int household, Money increment) =>
        increment.IsNegative
            ? books.Transfer(Account.Pool, Account.Household(household), -increment, TransferReason.Purchase)
            : books.Transfer(Account.Household(household), Account.Pool, increment, TransferReason.Purchase);
}
