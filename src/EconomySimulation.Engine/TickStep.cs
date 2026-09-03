namespace EconomySimulation.Engine;

/// <summary>
/// The seven steps of a tick, in the order `01-SIMULATION.md` §6 gives them.
///
/// Three of the orderings have a failure mode that produces a working run with wrong numbers,
/// which is why they are pinned before anything fills them in:
///
/// **Debt service before the walk** is what makes the affordability test at origination
/// sufficient — the instalment is guaranteed to fit because it came out of income before anything
/// else could claim it. Move it after and households spend their way into arrears, which v1 has no
/// machinery to handle.
///
/// **Ageing after the walk**, so a unit bought this tick starts at age 0 and is not immediately
/// wanted again.
///
/// **Repricing after the walk**, on this tick's demand, applying from the next tick.
/// </summary>
public enum TickStep
{
    /// <summary>Step 1 — each household receives `income_h` from the pool.</summary>
    Income,

    /// <summary>Step 2 — instalments come out before any shopping.</summary>
    DebtService,

    /// <summary>Step 3 — what each household wants this tick, in units, never budgets.</summary>
    Wants,

    /// <summary>Step 4 — households in a seeded random order, candidates ranked by score.</summary>
    Walk,

    /// <summary>Step 5 — every held durable gets a tick older.</summary>
    Ageing,

    /// <summary>Step 6 — eighteen prices move on their own excess demand.</summary>
    Repricing,

    /// <summary>Step 7 — V1, and the halt.</summary>
    Check,
}
