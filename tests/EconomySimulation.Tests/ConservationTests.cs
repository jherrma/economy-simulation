using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Ledger;

namespace EconomySimulation.Tests;

/// <summary>
/// V1. See spec/stories/03-02.
///
/// This is the project's white-furnace test: one invariant, trivially cheap, that any
/// money-creating or money-destroying bug violates on the tick it is made. Every story from here
/// on has to leave it green.
/// </summary>
public sealed class ConservationTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    private static Ledger Books(long households = 4, long cashEach = 1_000_00, long pool = 100_000_00)
    {
        var cash = new Money[households];
        Array.Fill(cash, new Money(cashEach));

        return Ledger.Open(cash, new Money(pool));
    }

    // ---- green ------------------------------------------------------------------------------

    /// <summary>
    /// 360 ticks, every scenario, both settings of money_creation. Nothing happens in them yet,
    /// which is exactly what makes them worth running: anything that accumulates when it should
    /// not shows up here, and they take milliseconds.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void AFullRunKeepsTheIdentity(bool creditEnabled, bool moneyCreation)
    {
        var parameters = Defaults with
        {
            Credit = Defaults.Credit with
            {
                CreditEnabled = creditEnabled,
                MoneyCreation = moneyCreation,
            },
        };

        var simulation = new Simulation(parameters, runSeed: 7);
        var run = simulation.Run();

        Assert.True(run.IsSuccess, run.IsFailed ? run.Errors[0].Message : "");
        Assert.Equal(360, simulation.Tick);
    }

    [Fact]
    public void WithCreditOff_LoansOutstandingIsZeroAtEveryTick()
    {
        var simulation = new Simulation(Defaults, runSeed: 7);

        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= 24; tick++)
        {
            Assert.True(simulation.RunTick(tick).IsSuccess);
            Assert.Equal(Money.Zero, simulation.Books.LoansOutstanding);
            Assert.Equal(Money.Zero, simulation.Books.NetMoneyCreated);
        }
    }

    // ---- the check actually fires -------------------------------------------------------------

    /// <summary>
    /// **The deliberate break.** Interest destroyed along with principal.
    ///
    /// This is the failure the story singles out, because it does not break the model — it biases
    /// it. The money stock drifts down under high credit, which damps the very effect being
    /// measured, and every other number stays plausible. Here it is done on purpose so that the
    /// check can be seen to fire, with the discrepancy named in cents.
    ///
    /// A conservation test that passes because it is summing the wrong array is worse than none,
    /// because it will be trusted.
    ///
    /// **And it is not the conservation sum that catches it.** Destroying money lowers both sides
    /// of `Σ cash + pool == M0 + net_created` at once, so the sum stays perfectly balanced through
    /// this bug. What catches it is that destroyed money has to be matched by a released claim —
    /// the assertion added in `01-SIMULATION.md` §7.1. Had V1 been implemented as the sum alone,
    /// as §6 step 7 states it, this run would have completed green.
    /// </summary>
    [Fact]
    public void DestroyingInterestAlongWithPrincipal_HaltsTheRun()
    {
        var books = Books();
        books.OpenTick();

        var principal = new Money(900_00);
        var interest = new Money(72_00);   // 8%/a over twelve months

        Assert.True(books.CreateMoney(Account.Household(0), principal, TransferReason.LoanOrigination).IsSuccess);
        books.AddClaim(principal);

        // The correct repayment: principal destroyed, interest transferred.
        Assert.True(books.DestroyMoney(Account.Household(0), principal, TransferReason.RepaymentPrincipal).IsSuccess);
        books.ReleaseClaim(principal);
        Assert.True(books.Check(tick: 1, moneyCreation: true).IsSuccess);

        // The bug: the interest destroyed too.
        Assert.True(books.DestroyMoney(Account.Household(0), interest, TransferReason.RepaymentPrincipal).IsSuccess);

        var check = books.Check(tick: 1, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("money creation, tick 1", check.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("a discrepancy of -7200 cents", check.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("RepaymentPrincipal 972.00", check.Errors[0].Message, StringComparison.Ordinal);

        // The sum itself never noticed, which is the whole point of the paragraph above.
        Assert.Equal(books.M0 + books.NetMoneyCreated, books.MoneyHeld);
    }

    /// <summary>Money conjured into an account with no claim behind it is caught the same tick.</summary>
    [Fact]
    public void MoneyAppearingFromNowhere_HaltsTheRun()
    {
        var books = Books();

        Assert.True(books.CreateMoney(Account.Household(0), new Money(1), TransferReason.LoanOrigination).IsSuccess);

        var check = books.Check(tick: 5, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("money creation, tick 5", check.Errors[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A loan funded from the pool while `money_creation` is on. The money stock is untouched, so
    /// the conservation sum alone would never notice — this is the assertion that does.
    /// </summary>
    [Fact]
    public void ALoanFundedTheWrongWay_HaltsTheRun()
    {
        var books = Books();
        var principal = new Money(900_00);

        Assert.True(books.Transfer(Account.Pool, Account.Household(0), principal, TransferReason.LoanOrigination).IsSuccess);
        books.AddClaim(principal);

        var check = books.Check(tick: 3, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("money creation, tick 3", check.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("900.00", check.Errors[0].Message, StringComparison.Ordinal);
    }

    /// <summary>And the mirror image: money created while the switch says it should not be.</summary>
    [Fact]
    public void MoneyCreatedWhileTheSwitchIsOff_HaltsTheRun()
    {
        var books = Books();
        var principal = new Money(900_00);

        Assert.True(books.CreateMoney(Account.Household(0), principal, TransferReason.LoanOrigination).IsSuccess);
        books.AddClaim(principal);

        var check = books.Check(tick: 3, moneyCreation: false);

        Assert.True(check.IsFailed);
        Assert.Contains("money_creation off", check.Errors[0].Message, StringComparison.Ordinal);
    }

    // ---- the bounds -----------------------------------------------------------------------------

    /// <summary>
    /// A negative pool halts with its own message. It is a calibration failure — income exceeds
    /// what the economy can pay — not a code failure, and conflating the two costs a day.
    /// </summary>
    [Fact]
    public void ANegativePool_HaltsWithACalibrationMessage()
    {
        var cash = new Money[1];
        cash[0] = new Money(100_00);
        var books = Ledger.Open(cash, new Money(-1));

        var check = books.Check(tick: 9, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("calibration result, not a bug", check.Errors[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("discrepancy", check.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANegativeCashBalance_HaltsNamingTheHousehold()
    {
        var cash = new Money[3];
        cash[0] = new Money(100_00);
        cash[1] = new Money(-50);
        cash[2] = new Money(100_00);
        var books = Ledger.Open(cash, new Money(1_000_00));

        var check = books.Check(tick: 4, moneyCreation: true);

        Assert.True(check.IsFailed);
        Assert.Contains("household 1 cash, tick 4", check.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHaltMessageCarriesThePerReasonTotals()
    {
        var books = Books();
        books.OpenTick();

        Assert.True(books.Transfer(Account.Pool, Account.Household(0), new Money(650_00), TransferReason.Income).IsSuccess);
        Assert.True(books.CreateMoney(Account.Household(0), new Money(5), TransferReason.LoanOrigination).IsSuccess);

        var message = books.Check(tick: 2, moneyCreation: false).Errors[0].Message;

        Assert.Contains("Income 650.00", message, StringComparison.Ordinal);
        Assert.Contains("LoanOrigination 0.05", message, StringComparison.Ordinal);
        Assert.Contains("created 0.05", message, StringComparison.Ordinal);
    }
}
