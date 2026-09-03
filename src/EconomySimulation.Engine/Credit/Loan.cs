namespace EconomySimulation.Engine.Credit;

/// <summary>
/// One loan: a principal, a total of interest, a term, and how many instalments have been paid.
/// A flat value in an array — households do not own object graphs.
///
/// **Simple interest**, deliberately: the total of interest is the principal times the rate over
/// the term, the conversion living in <see cref="Rate"/> and nowhere else. The difference from an annuity is
/// second-order for this question, and the arithmetic stays checkable by hand, which matters a
/// great deal while the model is being brought up. Replace it with an annuity when arrears exist
/// and the split between principal and interest starts to matter behaviourally.
///
/// **The parts are computed, not stored.** The story asks for a stored principal part and interest
/// part, but `principal / term` is not a whole number of cents in general, and a single stored
/// figure paid `term` times would sum to a cent more or less than the principal that was created —
/// which is V1 failing somewhere between one and twenty-four months later, with the trail cold.
/// So each instalment is the k-th part of the remainder-distributing split (01-02): the parts sum
/// to the principal and to the interest **exactly**, and the k-th instalment is the sum of the two.
/// Instalments therefore differ by at most two cents across the term. The one interest figure per
/// instalment is <see cref="InterestPart"/>; there is no separate accrual.
/// </summary>
/// <param name="Household">The borrower.</param>
/// <param name="Category">What the increment was for.</param>
/// <param name="Principal">What was lent: the cash increment, never the whole tier price.</param>
/// <param name="InterestTotal">`Rate.InterestOn(principal, term)`, fixed at origination.</param>
/// <param name="Term">Months.</param>
/// <param name="Paid">Instalments paid so far. `Paid == Term` is a retired loan.</param>
public readonly record struct Loan(
    int Household,
    int Category,
    Money Principal,
    Money InterestTotal,
    int Term,
    int Paid)
{
    public int RemainingTerm => Term - Paid;

    /// <summary>A loan with nothing left to pay stops being counted the moment its last instalment clears.</summary>
    public bool IsRetired => Paid >= Term;

    /// <summary>The principal in instalment <paramref name="k"/>. Over the term, these sum to the principal exactly.</summary>
    public Money PrincipalPart(int k) => Principal.Share(Term, k);

    /// <summary>The interest in instalment <paramref name="k"/>. Over the term, these sum to the interest exactly.</summary>
    public Money InterestPart(int k) => InterestTotal.Share(Term, k);

    /// <summary>Instalment <paramref name="k"/>: its principal part plus its interest part, to the cent.</summary>
    public Money Instalment(int k) => PrincipalPart(k) + InterestPart(k);

    /// <summary>What falls due next: the instalment the household's debt service counts this loan at.</summary>
    public Money NextInstalment => Instalment(Paid);

    /// <summary>The same loan, one instalment on.</summary>
    public Loan AfterPayment() => this with { Paid = Paid + 1 };

    /// <summary>A new loan for <paramref name="principal"/> at <paramref name="rate"/> over <paramref name="term"/> months.</summary>
    public static Loan Originate(int household, int category, Money principal, Rate rate, int term)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(term, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(principal.Cents);

        return new Loan(household, category, principal, rate.InterestOn(principal, term), term, Paid: 0);
    }
}
