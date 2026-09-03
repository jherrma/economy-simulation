using System.Globalization;
using System.Text;
using FluentResults;

namespace EconomySimulation.Engine.Ledger;

/// <summary>
/// Every euro in the town, and the only operations that move one.
///
/// There are exactly four movements in this model — income, purchase, loan origination, repayment
/// of principal — and no fifth may be added without changing `01-SIMULATION.md` §6. Interest is a
/// transfer, not a destruction: only the principal part of an instalment reduces the money stock.
/// Destroying interest along with it would drift the money stock down under high credit and damp
/// the very effect being measured, which biases the answer rather than breaking it.
/// </summary>
public sealed class Ledger
{
    private readonly Money[] householdCash;
    private readonly Money[] pool = new Money[1];
    private readonly Money[][] moneyHolders;
    private readonly Money[] movedByReason = new Money[Enum.GetValues<TransferReason>().Length];

    private Ledger(Money[] openingCash, Money openingPool)
    {
        householdCash = openingCash;
        pool[0] = openingPool;

        // The conservation sum walks this, so a new kind of holder is a new entry here and no
        // change at all to the check.
        moneyHolders = [householdCash, pool];

        M0 = MoneyHeld;
    }

    /// <summary>
    /// The opening money stock: `Σ cash_h + pool`, **computed**, never configured.
    ///
    /// It is the right-hand side of V1 for the rest of the run, so it is taken from the balances
    /// that actually exist rather than from the nominal figure in the specification — which is
    /// computed at the mean income and therefore differs from any particular draw.
    /// </summary>
    public Money M0 { get; }

    /// <summary>The bank's claim. A claim, not money: it sits on the other side of the identity.</summary>
    public Money LoansOutstanding { get; private set; }

    /// <summary>
    /// Net money brought into existence since the run began.
    ///
    /// This is what V1 balances against, rather than <see cref="LoansOutstanding"/> directly, so
    /// that the identity holds at **both** settings of `money_creation` — see
    /// `01-SIMULATION.md` §7.1. With creation off, loans exist and this stays zero.
    /// </summary>
    public Money NetMoneyCreated { get; private set; }

    public Money Created { get; private set; }

    public Money Destroyed { get; private set; }

    public int HouseholdCount => householdCash.Length;

    public Money Pool => pool[0];

    public Money Cash(int household) => householdCash[household];

    /// <summary>Every euro currently held, over every kind of holder.</summary>
    public Money MoneyHeld
    {
        get
        {
            var total = Money.Zero;

            foreach (var balances in moneyHolders)
            {
                foreach (var balance in balances)
                {
                    total += balance;
                }
            }

            return total;
        }
    }

    public Money MovedFor(TransferReason reason) => movedByReason[(int)reason];

    /// <summary>Opens the ledger from a drawn population.</summary>
    public static Ledger Open(Money[] openingCash, Money openingPool)
    {
        ArgumentNullException.ThrowIfNull(openingCash);

        return new Ledger([.. openingCash], openingPool);
    }

    // ---- the only things that change a balance -------------------------------------------

    /// <summary>
    /// Moves money from one account to another. The only operation that changes a balance without
    /// changing the money stock, and the only one used for income, purchases and interest.
    ///
    /// An overdraw fails. It does not clamp and it does not warn: clamping would silently create
    /// money, and a warning in a run of thirty seeds is a line nobody reads.
    /// </summary>
    public Result Transfer(Account from, Account to, Money amount, TransferReason reason)
    {
        if (amount.IsNegative)
        {
            return Result.Fail(
                $"transfer for {reason}: expected an amount of zero or more, got {amount.ToCsv()}");
        }

        var available = Balance(from);

        if (available < amount)
        {
            return Result.Fail(
                $"transfer for {reason} from {from} to {to}: expected at most the {available.ToCsv()} "
                + $"available, got {amount.ToCsv()}");
        }

        Set(from, available - amount);
        Set(to, Balance(to) + amount);
        movedByReason[(int)reason] += amount;

        return Result.Ok();
    }

    /// <summary>
    /// Brings money into existence in an account, against a claim of the same size.
    ///
    /// Deliberately not a transfer. A loan origination is not a transfer from anywhere — that is
    /// the point of it — and an implementation that models it as a transfer from the pool has
    /// quietly built the `money_creation = false` variant and shipped it as the default.
    /// </summary>
    public Result CreateMoney(Account into, Money amount, TransferReason reason)
    {
        if (amount.IsNegative)
        {
            return Result.Fail($"money creation for {reason}: expected zero or more, got {amount.ToCsv()}");
        }

        Set(into, Balance(into) + amount);
        NetMoneyCreated += amount;
        Created += amount;
        movedByReason[(int)reason] += amount;

        return Result.Ok();
    }

    /// <summary>Takes money out of existence, as repaid principal does.</summary>
    public Result DestroyMoney(Account from, Money amount, TransferReason reason)
    {
        if (amount.IsNegative)
        {
            return Result.Fail($"money destruction for {reason}: expected zero or more, got {amount.ToCsv()}");
        }

        var available = Balance(from);

        if (available < amount)
        {
            return Result.Fail(
                $"money destruction for {reason} from {from}: expected at most the "
                + $"{available.ToCsv()} available, got {amount.ToCsv()}");
        }

        Set(from, available - amount);
        NetMoneyCreated -= amount;
        Destroyed += amount;
        movedByReason[(int)reason] += amount;

        return Result.Ok();
    }

    /// <summary>Records the bank's claim growing. A claim is not money and moves no balance.</summary>
    public void AddClaim(Money principal)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(principal.Cents, 0);

        LoansOutstanding += principal;
    }

    /// <summary>Records the bank's claim shrinking as principal comes back.</summary>
    public void ReleaseClaim(Money principal)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(principal.Cents, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(principal.Cents, LoansOutstanding.Cents);

        LoansOutstanding -= principal;
    }

    /// <summary>Clears the per-reason totals at the start of a tick.</summary>
    public void OpenTick()
    {
        Array.Clear(movedByReason);
        Created = Money.Zero;
        Destroyed = Money.Zero;
    }

    // ---- V1 -------------------------------------------------------------------------------

    /// <summary>
    /// `Σ cash_h + pool == M0 + net_money_created`, to the cent, plus the bounds.
    ///
    /// This is the project's white-furnace test: one invariant, trivially cheap, that any
    /// money-creating or money-destroying bug violates on the tick it is made. It reports the
    /// discrepancy in cents and the per-reason totals, so the guilty operation is named rather
    /// than guessed at.
    ///
    /// The sum alone is **not** sufficient, and it is worth being exact about why. Every operation
    /// here maintains it by construction — destroying money lowers both sides at once — so a
    /// destruction that should have been a transfer leaves the sum perfectly balanced. What
    /// catches that is the second assertion below: money destroyed has to be matched by a claim
    /// released. Interest destroyed along with principal, the bug that damps the very effect this
    /// project measures, is caught there and nowhere else.
    /// </summary>
    public Result Check(int tick, bool moneyCreation)
    {
        var held = MoneyHeld;
        var expected = M0 + NetMoneyCreated;

        if (held != expected)
        {
            return Result.Fail(
                $"money conservation, tick {tick}: expected {expected.ToCsv()}, got {held.ToCsv()} "
                + $"— a discrepancy of {(held - expected).Cents} cents. {Diagnostics()}");
        }

        // Where the money_creation switch is actually policed. A loan funded the wrong way leaves
        // the money stock untouched, so nothing above would notice it.
        var claimed = moneyCreation ? LoansOutstanding : Money.Zero;

        if (NetMoneyCreated != claimed)
        {
            return Result.Fail(
                $"money creation, tick {tick}: expected {claimed.ToCsv()} with money_creation "
                + $"{(moneyCreation ? "on" : "off")}, got {NetMoneyCreated.ToCsv()} — a discrepancy "
                + $"of {(NetMoneyCreated - claimed).Cents} cents. {Diagnostics()}");
        }

        // A negative pool is a calibration failure, not a code failure. Conflating the two costs a
        // day, so it says which one it is.
        if (Pool.IsNegative)
        {
            return Result.Fail(
                $"the seller pool, tick {tick}: expected zero or more, got {Pool.ToCsv()}. This is a "
                + "calibration result, not a bug: income exceeds what the economy can pay. Report it "
                + "as a finding about the parameters.");
        }

        for (var h = 0; h < householdCash.Length; h++)
        {
            if (householdCash[h].IsNegative)
            {
                return Result.Fail(
                    $"household {h} cash, tick {tick}: expected zero or more, got "
                    + $"{householdCash[h].ToCsv()}. {Diagnostics()}");
            }
        }

        if (LoansOutstanding.IsNegative)
        {
            return Result.Fail(
                $"loans outstanding, tick {tick}: expected zero or more, got {LoansOutstanding.ToCsv()}");
        }

        return Result.Ok();
    }

    private string Diagnostics()
    {
        var report = new StringBuilder("Moved this tick: ");

        foreach (var reason in Enum.GetValues<TransferReason>())
        {
            report.Append(CultureInfo.InvariantCulture, $"{reason} {MovedFor(reason).ToCsv()}; ");
        }

        report.Append(CultureInfo.InvariantCulture, $"created {Created.ToCsv()}; destroyed {Destroyed.ToCsv()}.");

        return report.ToString();
    }

    private Money Balance(Account account) => Balances(account.Kind)[account.Index];

    private void Set(Account account, Money value) => Balances(account.Kind)[account.Index] = value;

    // CS8524 only: see Configuration/Sections.cs for why the discard arm is refused. Adding a
    // kind of holder must break this build, which is CS8509 and stays armed.
#pragma warning disable CS8524
    private Money[] Balances(AccountKind kind) => kind switch
    {
        AccountKind.HouseholdCash => householdCash,
        AccountKind.Pool => pool,
    };
#pragma warning restore CS8524
}
