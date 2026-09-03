using System.Globalization;

namespace EconomySimulation.Engine;

/// <summary>
/// An interest rate, carrying its units: per cent, per annum. The specification quotes
/// <c>loan_rate</c> as 8.0 per cent per annum and the model needs it as a multiplier over a term
/// in months, and the conversion between those two is where this kind of model goes quietly wrong.
///
/// Written out at a call site, that conversion is <c>rate / 100 * term / 12</c>, and getting it
/// wrong does not crash: it produces a plausible number. An earlier draft of this model had
/// <c>r_annual / 12</c> where the column was already in per cent — twenty-five per cent a month,
/// and a credit effect that looks spectacular and is arithmetic.
///
/// So there is one exit, <see cref="FinanceMultiplier"/>, the divisor appears once, and a test
/// scans the rest of the engine to make sure it stays that way.
/// </summary>
public readonly record struct Rate(double PercentPerAnnum)
{
    /// <summary>
    /// Per cent, per annum, per month: the single divisor in this model's interest arithmetic.
    /// It appears here and nowhere else, and RateTests fails the build if it turns up elsewhere.
    /// </summary>
    private const double PerCentPerAnnumPerMonth = 1200.0;

    public static readonly Rate Zero = new(0.0);

    /// <summary>
    /// Simple interest over a term in months: <c>1 + PercentPerAnnum · term / 1200</c>.
    ///
    /// 8 per cent per annum is 1.08 over twelve months and 1.16 over twenty-four. Not compound:
    /// the specification says simple interest, and a consumer instalment loan quoted this way is
    /// how the model's households are meant to see it.
    /// </summary>
    public double FinanceMultiplier(int termMonths)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(termMonths);

        // Multiply before dividing: the product is exact for the values this model uses, so the
        // only rounding is the final division.
        return 1.0 + (PercentPerAnnum * termMonths / PerCentPerAnnumPerMonth);
    }

    /// <summary>
    /// What a principal costs by the end of the term, principal included. This is the only way a
    /// rate reaches an amount of money: rounded once, through Money's own rounding, so that the
    /// instalments cut from it later add back up to it.
    /// </summary>
    public Money TotalRepayable(Money principal, int termMonths) =>
        principal.Scaled(FinanceMultiplier(termMonths));

    /// <summary>The interest alone — what the bank earns and pays straight back out.</summary>
    public Money InterestOn(Money principal, int termMonths) =>
        TotalRepayable(principal, termMonths) - principal;

    public override string ToString() =>
        PercentPerAnnum.ToString("0.###", CultureInfo.InvariantCulture) + "%/a";
}
