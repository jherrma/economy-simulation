namespace EconomySimulation.Engine.Ledger;

/// <summary>
/// A kind of place money can be — not a kind of agent.
///
/// The conservation sum is over kinds, so adding a holder later (firms, a second bank, a state) is
/// a new kind and a new array, and the check that money is conserved does not change. A ledger
/// that knew about *agent types* would need editing every time the model grew one.
/// </summary>
public enum AccountKind
{
    /// <summary>One balance per household.</summary>
    HouseholdCash,

    /// <summary>
    /// The seller pool: one account, standing in for the whole supply side. It has no behaviour.
    /// Its only job is to close the money circuit so conservation is checkable.
    /// </summary>
    Pool,
}

/// <summary>One account: a kind, and which one of that kind.</summary>
public readonly record struct Account(AccountKind Kind, int Index)
{
    public static Account Household(int household) => new(AccountKind.HouseholdCash, household);

    public static Account Pool { get; } = new(AccountKind.Pool, 0);

    public override string ToString() =>
        Kind == AccountKind.Pool ? "pool" : $"household {Index}";
}

/// <summary>
/// Why money moved. Closed, and named on every transfer.
///
/// Naming the reason costs nothing and buys the tick's diagnostics: when V1 breaks, the per-reason
/// totals say *which* operation lost the money rather than only that some did.
/// </summary>
public enum TransferReason
{
    /// <summary>Pool to household, every tick. No effect on the money stock.</summary>
    Income,

    /// <summary>Household to pool. No effect on the money stock.</summary>
    Purchase,

    /// <summary>Interest, back out to all households pro rata. A transfer, never a destruction.</summary>
    InterestDividend,

    /// <summary>New money in the borrower's hands — or, with creation off, money out of the pool.</summary>
    LoanOrigination,

    /// <summary>Principal, destroyed — or, with creation off, returned to the pool.</summary>
    RepaymentPrincipal,
}
