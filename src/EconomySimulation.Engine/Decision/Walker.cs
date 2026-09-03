using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Engine.Decision;

/// <summary>What happened to one candidate for one household.</summary>
public enum WalkOutcome
{
    /// <summary>Paid for, and the category's chosen tier moved up a step.</summary>
    Taken,

    /// <summary>Willing and able, and the shelf was empty. Counted as demand.</summary>
    Blocked,

    /// <summary>Willing, and could not pay. Not demand.</summary>
    Unaffordable,
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
    private readonly int runSeed;

    private readonly RandomStream orderStream;
    private readonly int[] order;
    private readonly double[] keenness;
    private readonly Candidate[] candidates;
    private readonly bool[] visited;
    private readonly int[] chosenTier;

    public Walker(
        SimulationParameters parameters,
        GoodsTable goods,
        Market market,
        Households population,
        Ledger.Ledger books,
        int runSeed)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(goods);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(books);

        this.parameters = parameters;
        this.goods = goods;
        this.market = market;
        this.population = population;
        this.books = books;
        this.runSeed = runSeed;

        orderStream = RandomStream.ForTick(runSeed, 0, Purpose.Order);
        order = new int[population.Count];
        keenness = new double[population.Count];
        candidates = new Candidate[goods.GoodCount];
        visited = new bool[goods.GoodCount];
        chosenTier = new int[goods.CategoryCount];
    }

    /// <summary>Set by tests to watch outcomes. Leave null in a run.</summary>
    internal WalkObserver? Observer { get; set; }

    /// <summary>The order the last tick visited households in, for tests of the shuffle.</summary>
    internal ReadOnlySpan<int> LastOrder => order;

    public Result Run(int tick)
    {
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

        var lambda = parameters.Decision.Lambda;

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
            // not have paid would not have bought from a full shelf either.
            if (candidate.DeltaPrice > books.Cash(household))
            {
                market.RecordUnaffordable(candidate.Category, candidate.Tier);
                Observer?.Invoke(household, in candidate, WalkOutcome.Unaffordable);
                continue;
            }

            if (market.Stock(candidate.Category, candidate.Tier) == 0)
            {
                market.RecordBlocked(candidate.Category, candidate.Tier);
                Observer?.Invoke(household, in candidate, WalkOutcome.Blocked);
                continue;
            }

            var paid = Pay(household, candidate.DeltaPrice);

            if (!Results.IsOk(paid))
            {
                return paid;
            }

            chosenTier[candidate.Category] = candidate.Tier;
            Observer?.Invoke(household, in candidate, WalkOutcome.Taken);
        }

        // Stock is consumed once per category, at the final tier, and the unit is new.
        for (var c = 0; c < goods.CategoryCount; c++)
        {
            if (chosenTier[c] >= 0)
            {
                market.Sell(c, chosenTier[c]);
                population.Acquire(household, c);
            }
        }

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
