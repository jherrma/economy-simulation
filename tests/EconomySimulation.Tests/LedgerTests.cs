using EconomySimulation.Engine;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/03-01.</summary>
public sealed class LedgerTests
{
    private static Ledger Open(int households = 4, long cashEach = 100_00, long pool = 10_000_00)
    {
        var cash = new Money[households];
        Array.Fill(cash, new Money(cashEach));

        return Ledger.Open(cash, new Money(pool));
    }

    // ---- M0 and the conservation sum ------------------------------------------------------

    [Fact]
    public void M0IsTheOpeningBalances()
    {
        var books = Open();

        Assert.Equal(new Money(4 * 100_00 + 10_000_00), books.M0);
        Assert.Equal(books.M0, books.MoneyHeld);
        Assert.Equal(Money.Zero, books.LoansOutstanding);
        Assert.Equal(Money.Zero, books.NetMoneyCreated);
    }

    /// <summary>
    /// A few thousand transfers in a random order leave the sum exactly where it started.
    ///
    /// The point is the *exactly*. A floating-point ledger passes a version of this test with a
    /// tolerance, and the bugs the invariant exists to catch are usually smaller than the
    /// tolerance anyone would pick.
    /// </summary>
    [Fact]
    public void ThousandsOfTransfers_LeaveTheSumUnchanged()
    {
        var books = Open(households: 50, cashEach: 5_000_00);
        var opening = books.MoneyHeld;
        var stream = RandomStream.ForTick(99, 1, Purpose.Order);

        for (var i = 0; i < 5_000; i++)
        {
            var household = Account.Household(stream.NextInt(books.HouseholdCount));
            var amount = new Money(stream.NextInt(20_000));

            var toPool = stream.NextInt(2) == 0;

            var moved = toPool
                ? books.Transfer(household, Account.Pool, amount, TransferReason.Purchase)
                : books.Transfer(Account.Pool, household, amount, TransferReason.Income);

            // An overdraw is refused, which is a legitimate outcome of a random amount.
            _ = moved;

            Assert.Equal(opening, books.MoneyHeld);
        }

        Assert.True(books.Check(tick: 1, moneyCreation: true).IsSuccess);
    }

    // ---- overdraw ---------------------------------------------------------------------------

    /// <summary>
    /// An overdraw fails. It does not clamp — clamping silently creates money — and it does not
    /// warn, because a warning in a run of thirty seeds is a line nobody reads.
    /// </summary>
    [Fact]
    public void ATransferThatWouldOverdraw_Fails()
    {
        var books = Open();

        var moved = books.Transfer(
            Account.Household(0),
            Account.Pool,
            new Money(100_01),
            TransferReason.Purchase);

        Assert.True(moved.IsFailed);
        Assert.Equal(new Money(100_00), books.Cash(0));
        Assert.Contains("100.01", moved.Errors.Single().Message, StringComparison.Ordinal);
        Assert.Contains("100.00", moved.Errors.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATransferOfExactlyTheBalance_Succeeds()
    {
        var books = Open();

        Assert.True(books.Transfer(Account.Household(0), Account.Pool, new Money(100_00), TransferReason.Purchase).IsSuccess);
        Assert.Equal(Money.Zero, books.Cash(0));
    }

    [Fact]
    public void ANegativeTransfer_Fails()
    {
        var books = Open();

        Assert.True(books.Transfer(Account.Pool, Account.Household(0), new Money(-1), TransferReason.Income).IsFailed);
    }

    // ---- creation and destruction are not transfers -----------------------------------------

    /// <summary>
    /// A loan origination is not a transfer from anywhere — that is the point of it. An
    /// implementation that models it as a transfer from the pool has quietly built the
    /// `money_creation = false` variant and shipped it as the default.
    /// </summary>
    [Fact]
    public void CreatingMoney_RaisesTheStock_AndComesFromNoAccount()
    {
        var books = Open();
        var poolBefore = books.Pool;

        Assert.True(books.CreateMoney(Account.Household(0), new Money(900_00), TransferReason.LoanOrigination).IsSuccess);
        books.AddClaim(new Money(900_00));

        Assert.Equal(poolBefore, books.Pool);
        Assert.Equal(new Money(1_000_00), books.Cash(0));
        Assert.Equal(new Money(900_00), books.NetMoneyCreated);
        Assert.Equal(books.M0 + new Money(900_00), books.MoneyHeld);
        Assert.True(books.Check(tick: 1, moneyCreation: true).IsSuccess);
    }

    [Fact]
    public void DestroyingMoney_LowersTheStock()
    {
        var books = Open();

        Assert.True(books.CreateMoney(Account.Household(0), new Money(900_00), TransferReason.LoanOrigination).IsSuccess);
        books.AddClaim(new Money(900_00));

        Assert.True(books.DestroyMoney(Account.Household(0), new Money(300_00), TransferReason.RepaymentPrincipal).IsSuccess);
        books.ReleaseClaim(new Money(300_00));

        Assert.Equal(new Money(600_00), books.NetMoneyCreated);
        Assert.Equal(new Money(600_00), books.LoansOutstanding);
        Assert.True(books.Check(tick: 1, moneyCreation: true).IsSuccess);
    }

    [Fact]
    public void DestroyingMoreThanAnAccountHolds_Fails()
    {
        var books = Open();

        Assert.True(books.DestroyMoney(Account.Household(0), new Money(100_01), TransferReason.RepaymentPrincipal).IsFailed);
        Assert.Equal(new Money(100_00), books.Cash(0));
    }

    [Fact]
    public void ReleasingMoreClaimThanExists_IsARejectedArgument()
    {
        var books = Open();

        Assert.Throws<ArgumentOutOfRangeException>(() => books.ReleaseClaim(new Money(1)));
    }

    // ---- the per-reason diagnostics ------------------------------------------------------------

    /// <summary>
    /// Naming the reason on every movement costs nothing and buys the tick's diagnostics: when V1
    /// breaks, these say which operation lost the money rather than only that some did.
    /// </summary>
    [Fact]
    public void EveryMovementIsCountedAgainstItsReason()
    {
        var books = Open();
        books.OpenTick();

        Assert.True(books.Transfer(Account.Pool, Account.Household(0), new Money(650_00), TransferReason.Income).IsSuccess);
        Assert.True(books.Transfer(Account.Household(0), Account.Pool, new Money(300_00), TransferReason.Purchase).IsSuccess);
        Assert.True(books.Transfer(Account.Household(0), Account.Pool, new Money(200_00), TransferReason.Purchase).IsSuccess);

        Assert.Equal(new Money(650_00), books.MovedFor(TransferReason.Income));
        Assert.Equal(new Money(500_00), books.MovedFor(TransferReason.Purchase));
        Assert.Equal(Money.Zero, books.MovedFor(TransferReason.InterestDividend));
    }

    [Fact]
    public void TheDiagnosticsAreClearedAtTheStartOfATick()
    {
        var books = Open();
        books.OpenTick();
        Assert.True(books.Transfer(Account.Pool, Account.Household(0), new Money(1_00), TransferReason.Income).IsSuccess);

        books.OpenTick();

        Assert.Equal(Money.Zero, books.MovedFor(TransferReason.Income));
        Assert.Equal(Money.Zero, books.Created);
        Assert.Equal(Money.Zero, books.Destroyed);
    }

    // ---- balances have one home ------------------------------------------------------------------

    /// <summary>
    /// Derived quantities are computed, never stored. A stored net worth or debt service is a
    /// second source of truth, and the two will disagree at some point in a thirty-year run.
    /// </summary>
    [Fact]
    public void TheEngineStoresNoDerivedBalance()
    {
        string[] banned = ["NetWorth", "netWorth", "TotalCash", "totalCash", "CachedBalance"];

        var offenders = new List<string>();

        foreach (var path in Repo.EngineSources())
        {
            var text = File.ReadAllText(path);
            offenders.AddRange(banned.Where(b => text.Contains(b, StringComparison.Ordinal)).Select(b => $"{Path.GetFileName(path)}: {b}"));
        }

        Assert.True(offenders.Count == 0, "A derived quantity is stored: " + string.Join("; ", offenders));
    }

    /// <summary>
    /// No amount of money in a floating-point number, anywhere.
    ///
    /// The scan has to know the difference between an amount and a multiplier: `price_mult` and
    /// `opening_cash_share` are dimensionless and belong in a double, while a price or a balance
    /// never does. So a line naming a monetary quantity is only an offence when it does not also
    /// name the thing that makes it a ratio.
    /// </summary>
    [Fact]
    public void NoMonetaryFieldIsADouble()
    {
        string[] monetary = ["cash", "price", "pool", "balance", "principal", "instalment", "income"];
        string[] dimensionless = ["share", "mult", "rate", "ratio", "factor", "slope", "weight", "sigma", "score", "count"];

        var offenders = new List<string>();

        foreach (var path in Repo.EngineSources())
        {
            var lines = File.ReadAllLines(path);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var code = line.TrimStart();

                if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!line.Contains("double", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!monetary.Any(m => line.Contains(m, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (dimensionless.Any(d => line.Contains(d, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                offenders.Add($"{Path.GetFileName(path)}:{i + 1}: {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0, "Money in a double: " + string.Join("; ", offenders));
    }
}
