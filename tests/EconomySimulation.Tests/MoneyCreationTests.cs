using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Ledger;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/06-03. The only two operations that change the money stock, and the interest
/// that must not be a third. Loans are originated here by hand, the way the walk does it (06-02):
/// money created into the borrower's cash against a claim, spent into the pool, and the loan
/// entered in the book.
/// </summary>
public sealed class MoneyCreationTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private const int Electronics = 4;

    private static void Originate(Simulation simulation, int household, Money principal, int term = 24)
    {
        var books = simulation.Books;
        var borrower = Account.Household(household);

        Assert.True(books.CreateMoney(borrower, principal, TransferReason.LoanOrigination).IsSuccess);
        books.AddClaim(principal);
        Assert.True(books.Transfer(borrower, Account.Pool, principal, TransferReason.Purchase).IsSuccess);

        simulation.Loans.Add(Loan.Originate(household, Electronics, principal, simulation.Parameters.Credit.LoanRate, term));
    }

    private static Simulation Started(int seed = 1)
    {
        var simulation = new Simulation(Defaults, seed);
        Assert.True(simulation.Start().IsSuccess);

        return simulation;
    }

    /// <summary>
    /// A full credit cycle: fifty households borrow €900 over 24 months in tick 1, and V1 holds at
    /// the end of every one of the next thirty ticks. Origination created exactly the principal;
    /// repayment destroyed exactly the principal; at the end nothing is owed, nothing was made.
    /// </summary>
    [Fact]
    public void V1HoldsThroughOriginationRepaymentAndDistribution()
    {
        var simulation = Started();
        var books = simulation.Books;
        var principal = Money.FromEuros(900);

        for (var h = 0; h < 50; h++)
        {
            Originate(simulation, h, principal);
        }

        Assert.Equal(principal * 50, books.LoansOutstanding);
        Assert.Equal(books.M0 + (principal * 50), books.MoneyHeld);

        var repaid = Money.Zero;
        var interest = Money.Zero;

        for (var tick = 1; tick <= 30; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");

            repaid += simulation.Loans.LastPrincipalRepaid;
            interest += simulation.Loans.LastInterestCollected;

            // Nothing sits in the till between ticks, and V1 is asserted by the tick itself.
            Assert.True(books.Bank.IsZero);
            Assert.Equal(books.M0 + books.LoansOutstanding, books.MoneyHeld);
        }

        Assert.Equal(Money.Zero, books.LoansOutstanding);
        Assert.Equal(0, simulation.Loans.LiveCount);
        Assert.Equal(principal * 50, repaid);
        Assert.Equal(Money.FromEuros(144) * 50, interest);
        Assert.Equal(books.M0, books.MoneyHeld);
    }

    /// <summary>
    /// The interest part is a transfer: over one debt-service step the households' cash falls by
    /// principal plus interest, the money stock falls by the principal alone, and the whole of the
    /// interest is back in household hands — split by income, to the cent — before the step ends.
    /// </summary>
    [Fact]
    public void InterestIsATransfer_PaidOutProRataByIncomeToTheCent()
    {
        var simulation = Started();
        var books = simulation.Books;
        var population = simulation.Population;

        Originate(simulation, 0, Money.FromEuros(900));
        Originate(simulation, 1, Money.FromEuros(333.33m), term: 7);

        var interestDue = simulation.Loans.LoansOf(0)[0].InterestPart(0) + simulation.Loans.LoansOf(1)[0].InterestPart(0);
        var principalDue = simulation.Loans.LoansOf(0)[0].PrincipalPart(0) + simulation.Loans.LoansOf(1)[0].PrincipalPart(0);

        var before = Enumerable.Range(0, population.Count).Select(books.Cash).ToArray();
        var heldBefore = books.MoneyHeld;

        Assert.True(simulation.Loans.Service(books, moneyCreation: true).IsSuccess);

        Assert.Equal(heldBefore - principalDue, books.MoneyHeld);
        Assert.True(books.Bank.IsZero);
        Assert.Equal(interestDue, simulation.Loans.LastInterestCollected);

        var weights = population.Income.Select(i => (double)i.Cents).ToArray();
        var expectedDividend = interestDue.Allocate(weights);

        var dividendPaid = Money.Zero;

        for (var h = 0; h < population.Count; h++)
        {
            var instalment = h switch
            {
                0 => Loan.Originate(0, Electronics, Money.FromEuros(900), Defaults.Credit.LoanRate, 24).Instalment(0),
                1 => Loan.Originate(1, Electronics, Money.FromEuros(333.33m), Defaults.Credit.LoanRate, 7).Instalment(0),
                _ => Money.Zero,
            };

            var received = books.Cash(h) - before[h] + instalment;
            Assert.Equal(expectedDividend[h], received);
            dividendPaid += received;
        }

        Assert.Equal(interestDue, dividendPaid);
    }

    /// <summary>
    /// Abstainers receive the dividend too. They hold bank shares like anyone else; it biases
    /// mildly against the hypothesis, and it must not be quietly removed.
    /// </summary>
    [Fact]
    public void AbstainersReceiveTheDividend()
    {
        var simulation = Started();
        var books = simulation.Books;
        var population = simulation.Population;

        var abstainer = Enumerable.Range(0, population.Count).First(h => population.IsAbstainer[h]);

        // Every non-abstainer borrows €900, so the interest collected — €6 a loan — is enough for
        // every household's share to come to at least a cent.
        for (var h = 0; h < population.Count; h++)
        {
            if (!population.IsAbstainer[h])
            {
                Originate(simulation, h, Money.FromEuros(900));
            }
        }

        var cashBefore = books.Cash(abstainer);
        Assert.True(simulation.Loans.Service(books, moneyCreation: true).IsSuccess);

        var weights = population.Income.Select(i => (double)i.Cents).ToArray();
        var share = simulation.Loans.LastInterestCollected.Allocate(weights)[abstainer];

        Assert.True(share > Money.Zero);
        Assert.Equal(cashBefore + share, books.Cash(abstainer));
        Assert.Equal(0, simulation.Loans.LiveLoans(abstainer));
    }

    /// <summary>
    /// The deliberate failure. Destroy the interest along with the principal — the mistake this
    /// story exists to make impossible — and the check refuses the tick: money destroyed was not
    /// matched by a claim released. The conservation sum alone would have stayed balanced.
    /// </summary>
    [Fact]
    public void DestroyingTheInterestAsWell_FailsTheCheck()
    {
        var simulation = Started();
        var books = simulation.Books;
        var loan = Loan.Originate(0, Electronics, Money.FromEuros(900), Defaults.Credit.LoanRate, 24);

        Originate(simulation, 0, loan.Principal);

        // The wrong step 2: one destruction of the whole instalment, the claim released by the
        // principal part only.
        Assert.True(books.DestroyMoney(Account.Household(0), loan.Instalment(0), TransferReason.RepaymentPrincipal).IsSuccess);
        books.ReleaseClaim(loan.PrincipalPart(0));

        Assert.Equal(books.M0 + books.NetMoneyCreated, books.MoneyHeld);

        var check = books.Check(tick: 1, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("money creation, tick 1", check.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("-600 cents", check.Errors[0].Message, StringComparison.Ordinal);
    }

    /// <summary>Interest left in the till — collected and not paid out — is caught by the same check.</summary>
    [Fact]
    public void InterestLeftInTheTill_FailsTheCheck()
    {
        var simulation = Started();
        var books = simulation.Books;

        Assert.True(books.Transfer(Account.Household(0), Account.Bank, Money.FromEuros(6), TransferReason.InterestDividend).IsSuccess);

        var check = books.Check(tick: 1, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("the bank's till, tick 1", check.Errors[0].Message, StringComparison.Ordinal);
    }

    /// <summary>The debt-service step allocates nothing, even with a thousand loans live.</summary>
    [Fact]
    public void TheDebtServiceStepAllocatesNothing()
    {
        var simulation = Started();

        for (var h = 0; h < simulation.Population.Count; h++)
        {
            Originate(simulation, h, Money.FromEuros(900));
        }

        // Warm up: the first call may JIT.
        Assert.True(simulation.Loans.Service(simulation.Books, moneyCreation: true).IsSuccess);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = simulation.Loans.Service(simulation.Books, moneyCreation: true);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(result.IsSuccess);
        Assert.Equal(0, allocated);
    }
}
