using EconomySimulation.Engine.World;

namespace EconomySimulation.Engine.Credit;

/// <summary>
/// Every live loan in the town, as a flat array with a free list and one singly linked chain per
/// household. Nothing here is an object graph: a household's loans are the slots its chain visits.
///
/// `debt_service_h` is **computed** from the chain every time it is asked for, never stored. A
/// stored figure would be a second source of truth that drifts every time a loan retires, and it
/// is asked for inside the walk, where a running total that includes the loan just originated is
/// exactly what the residual test needs.
/// </summary>
public sealed class LoanBook
{
    private const int None = -1;

    private readonly int[] headOf;
    private Loan[] loans;
    private int[] nextOf;
    private int freeHead;

    public LoanBook(int householdCount, int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(householdCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, 1);

        headOf = new int[householdCount];
        Array.Fill(headOf, None);

        loans = new Loan[initialCapacity];
        nextOf = new int[initialCapacity];
        freeHead = None;
        Release(0, initialCapacity);
    }

    /// <summary>Loans not yet retired, over the whole town.</summary>
    public int LiveCount { get; private set; }

    /// <summary>Loans originated since the book opened, retired ones included.</summary>
    public int Originated { get; private set; }

    /// <summary>
    /// The sum of the next instalment on every live loan of the household — what falls due at
    /// the next debt-service step, and what the residual test in the walk deducts.
    /// </summary>
    public Money DebtService(int household)
    {
        var total = Money.Zero;

        for (var i = headOf[household]; i != None; i = nextOf[i])
        {
            total += loans[i].NextInstalment;
        }

        return total;
    }

    /// <summary>How many live loans the household carries.</summary>
    public int LiveLoans(int household)
    {
        var count = 0;

        for (var i = headOf[household]; i != None; i = nextOf[i])
        {
            count++;
        }

        return count;
    }

    /// <summary>Copies the household's live loans out, for tests and output. Allocates; never call it in the tick.</summary>
    public Loan[] LoansOf(int household)
    {
        var result = new Loan[LiveLoans(household)];
        var n = 0;

        for (var i = headOf[household]; i != None; i = nextOf[i])
        {
            result[n++] = loans[i];
        }

        return result;
    }

    /// <summary>Records a new loan. The money side — creation or transfer — is the ledger's business, not the book's.</summary>
    public void Add(Loan loan)
    {
        if (loan.IsRetired)
        {
            throw new ArgumentException("A loan with nothing to pay is not a loan.", nameof(loan));
        }

        if (freeHead == None)
        {
            Grow();
        }

        var slot = freeHead;
        freeHead = nextOf[slot];

        loans[slot] = loan;
        nextOf[slot] = headOf[loan.Household];
        headOf[loan.Household] = slot;

        LiveCount++;
        Originated++;
    }

    /// <summary>The slot the household's chain starts at, for the allocation-free debt-service loop.</summary>
    internal int First(int household) => headOf[household];

    internal int Next(int slot) => nextOf[slot];

    internal ref Loan At(int slot) => ref loans[slot];

    /// <summary>
    /// Unlinks <paramref name="slot"/>, which must follow <paramref name="previous"/> in the
    /// household's chain (or head it, if <paramref name="previous"/> is −1). Returns the slot
    /// after it, so a caller iterating the chain continues from there.
    /// </summary>
    internal int Retire(int household, int previous, int slot)
    {
        var next = nextOf[slot];

        if (previous == None)
        {
            headOf[household] = next;
        }
        else
        {
            nextOf[previous] = next;
        }

        nextOf[slot] = freeHead;
        freeHead = slot;
        LiveCount--;

        return next;
    }

    private void Grow()
    {
        // Rare by construction: the opening capacity covers one loan per tier per financeable
        // category per household, which is the most a household can hold while every loan's term
        // is at most its good's life. A configuration that stacks deeper pays one resize.
        var old = loans.Length;
        Array.Resize(ref loans, old * 2);
        Array.Resize(ref nextOf, old * 2);
        Release(old, old);
    }

    private void Release(int from, int count)
    {
        for (var i = from + count - 1; i >= from; i--)
        {
            nextOf[i] = freeHead;
            freeHead = i;
        }
    }
}
