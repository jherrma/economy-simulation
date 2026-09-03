using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;
using FluentResults;

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
    private readonly double[] dividendWeights;
    private readonly long[] dividendParts;
    private readonly double[] dividendFractions;
    private Loan[] loans;
    private int[] nextOf;
    private int freeHead;

    /// <summary>A book for a town of <paramref name="householdCount"/>, with no dividend weights: the service step needs the other constructor.</summary>
    public LoanBook(int householdCount, int initialCapacity)
        : this(Uniform(householdCount), initialCapacity)
    {
    }

    /// <summary>A book whose dividend is paid pro rata by the households' fixed incomes.</summary>
    public LoanBook(Households population, int initialCapacity)
        : this(IncomeWeights(population), initialCapacity)
    {
    }

    private LoanBook(double[] weights, int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, 1);

        dividendWeights = weights;
        dividendParts = new long[weights.Length];
        dividendFractions = new double[weights.Length];

        headOf = new int[weights.Length];
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

    /// <summary>Interest collected in the last debt-service step — and paid straight back out.</summary>
    public Money LastInterestCollected { get; private set; }

    /// <summary>Principal repaid in the last debt-service step: destroyed, or returned to the pool.</summary>
    public Money LastPrincipalRepaid { get; private set; }

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

    // ---- step 2 -------------------------------------------------------------------------------

    /// <summary>
    /// Step 2 — debt service, before any shopping. Every live loan pays its next instalment out of
    /// the borrower's cash: the principal part is **destroyed** (or, with `money_creation` off,
    /// returned to the pool it came from) and the bank's claim shrinks by exactly that; the
    /// interest part goes to the bank's till. The till is then paid out to every household pro
    /// rata by income, the remainder-distributing split making the parts sum to what was collected
    /// to the cent, and the till is empty again before step 3.
    ///
    /// Two things here decide the answer rather than break the run. Interest is a **transfer**:
    /// destroyed along with the principal, the money stock would leak downward at exactly the rate
    /// households borrow, damping the effect being measured. And abstainers receive the dividend
    /// like anyone else — they hold bank shares too — which biases mildly *against* the hypothesis
    /// and is the honest treatment.
    ///
    /// Cannot fail for want of cash by construction: the residual test at origination and this
    /// step's place before the walk make arrears impossible, so a failed transfer here is a bug,
    /// and the result says which household and why.
    /// </summary>
    public Result Service(Ledger.Ledger books, bool moneyCreation)
    {
        ArgumentNullException.ThrowIfNull(books);

        if (books.HouseholdCount != headOf.Length)
        {
            throw new ArgumentException("The ledger and the loan book describe different towns.", nameof(books));
        }

        LastInterestCollected = Money.Zero;
        LastPrincipalRepaid = Money.Zero;

        if (LiveCount == 0)
        {
            return Results.Ok;
        }

        for (var h = 0; h < headOf.Length; h++)
        {
            var previous = None;
            var slot = headOf[h];

            while (slot != None)
            {
                ref var loan = ref loans[slot];
                var principalPart = loan.PrincipalPart(loan.Paid);
                var interestPart = loan.InterestPart(loan.Paid);
                var borrower = Account.Household(h);

                var interest = books.Transfer(borrower, Account.Bank, interestPart, TransferReason.InterestDividend);

                if (!Results.IsOk(interest))
                {
                    return interest;
                }

                var principal = moneyCreation
                    ? books.DestroyMoney(borrower, principalPart, TransferReason.RepaymentPrincipal)
                    : books.Transfer(borrower, Account.Pool, principalPart, TransferReason.RepaymentPrincipal);

                if (!Results.IsOk(principal))
                {
                    return principal;
                }

                books.ReleaseClaim(principalPart);
                LastInterestCollected += interestPart;
                LastPrincipalRepaid += principalPart;

                loan = loan.AfterPayment();

                if (loan.IsRetired)
                {
                    slot = Retire(h, previous, slot);
                    continue;
                }

                previous = slot;
                slot = nextOf[slot];
            }
        }

        return Distribute(books);
    }

    /// <summary>The till, to every household pro rata by income, to the cent.</summary>
    private Result Distribute(Ledger.Ledger books)
    {
        var collected = books.Bank;

        if (collected.IsZero)
        {
            return Results.Ok;
        }

        Allocation.LargestRemainderInto(collected.Cents, dividendWeights, dividendParts, dividendFractions);

        for (var h = 0; h < dividendParts.Length; h++)
        {
            if (dividendParts[h] == 0)
            {
                continue;
            }

            var paid = books.Transfer(Account.Bank, Account.Household(h), new Money(dividendParts[h]), TransferReason.InterestDividend);

            if (!Results.IsOk(paid))
            {
                return paid;
            }
        }

        return Results.Ok;
    }

    private static double[] Uniform(int householdCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(householdCount);

        var weights = new double[householdCount];
        Array.Fill(weights, 1.0);

        return weights;
    }

    private static double[] IncomeWeights(Households population)
    {
        ArgumentNullException.ThrowIfNull(population);

        var weights = new double[population.Count];

        for (var h = 0; h < weights.Length; h++)
        {
            weights[h] = population.Income[h].Cents;
        }

        return weights;
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
