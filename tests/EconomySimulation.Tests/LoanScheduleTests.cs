using EconomySimulation.Engine;
using EconomySimulation.Engine.Credit;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/06-01. Simple interest, an exact split, and a debt service that is computed.</summary>
public sealed class LoanScheduleTests
{
    private static readonly Rate EightPerCent = new(8.0);

    /// <summary>€900 over 24 months at 8%: €144 of interest, €43.50 a month, and both parts sum exactly.</summary>
    [Fact]
    public void NineHundredEurosOverTwentyFourMonths_AmortisesExactly()
    {
        var loan = Loan.Originate(household: 0, category: 4, Money.FromEuros(900), EightPerCent, term: 24);

        Assert.Equal(Money.FromEuros(144), loan.InterestTotal);

        var principal = Money.Zero;
        var interest = Money.Zero;

        for (var k = 0; k < loan.Term; k++)
        {
            Assert.Equal(Money.FromEuros(43.50m), loan.Instalment(k));
            Assert.Equal(loan.Instalment(k), loan.PrincipalPart(k) + loan.InterestPart(k));

            principal += loan.PrincipalPart(k);
            interest += loan.InterestPart(k);
        }

        Assert.Equal(Money.FromEuros(900), principal);
        Assert.Equal(Money.FromEuros(144), interest);
    }

    /// <summary>
    /// An awkward principal: €333.33 over 7 months. Neither part divides, the instalments differ by
    /// a cent or two, and the parts still sum to the principal and to the interest exactly — which
    /// is what the walk's money side depends on, because the principal parts are what get destroyed.
    /// </summary>
    [Fact]
    public void AnAwkwardPrincipal_StillSumsToTheCent()
    {
        var loan = Loan.Originate(0, 3, Money.FromEuros(333.33m), EightPerCent, term: 7);

        var principal = Money.Zero;
        var interest = Money.Zero;
        var smallest = long.MaxValue;
        var largest = long.MinValue;

        for (var k = 0; k < loan.Term; k++)
        {
            principal += loan.PrincipalPart(k);
            interest += loan.InterestPart(k);
            smallest = Math.Min(smallest, loan.Instalment(k).Cents);
            largest = Math.Max(largest, loan.Instalment(k).Cents);
        }

        Assert.Equal(loan.Principal, principal);
        Assert.Equal(loan.InterestTotal, interest);
        Assert.InRange(largest - smallest, 0, 2);
        Assert.Equal(EightPerCent.InterestOn(loan.Principal, 7), loan.InterestTotal);
    }

    /// <summary>The k-th part in closed form is the k-th part of the array split, for every k.</summary>
    [Theory]
    [InlineData(100, 3)]
    [InlineData(90_000, 24)]
    [InlineData(33_333, 7)]
    [InlineData(-100, 3)]
    public void ShareAgreesWithSplit(long cents, int parts)
    {
        var amount = new Money(cents);
        var split = amount.Split(parts);

        for (var k = 0; k < parts; k++)
        {
            Assert.Equal(split[k], amount.Share(parts, k));
        }
    }

    /// <summary>Debt service is the sum of the next instalments on live loans, and a retired loan drops out of it.</summary>
    [Fact]
    public void DebtServiceIsComputed_AndARetiredLoanLeavesIt()
    {
        var book = new LoanBook(householdCount: 2, initialCapacity: 1);
        var hobby = Loan.Originate(0, 3, Money.FromEuros(360), EightPerCent, term: 12);
        var phone = Loan.Originate(0, 4, Money.FromEuros(540), EightPerCent, term: 24);

        book.Add(hobby);
        book.Add(phone);

        Assert.Equal(2, book.LiveCount);
        Assert.Equal(2, book.LiveLoans(0));
        Assert.Equal(0, book.LiveLoans(1));
        Assert.Equal(hobby.NextInstalment + phone.NextInstalment, book.DebtService(0));
        Assert.Equal(Money.Zero, book.DebtService(1));

        // Pay the hobby loan off in full.
        for (var k = 0; k < 12; k++)
        {
            var previous = -1;
            var slot = book.First(0);

            while (slot != -1)
            {
                ref var loan = ref book.At(slot);

                if (loan.Category == 3)
                {
                    loan = loan.AfterPayment();

                    if (loan.IsRetired)
                    {
                        slot = book.Retire(0, previous, slot);
                        continue;
                    }
                }

                previous = slot;
                slot = book.Next(slot);
            }
        }

        Assert.Equal(1, book.LiveCount);
        Assert.Equal(phone.NextInstalment, book.DebtService(0));
        Assert.Equal(2, book.Originated);
        Assert.Single(book.LoansOf(0));
        Assert.Equal(4, book.LoansOf(0)[0].Category);
    }

    /// <summary>The book was opened with one slot and took two loans, then a third: growth is a resize, not a refusal.</summary>
    [Fact]
    public void TheBookGrowsWhenFull()
    {
        var book = new LoanBook(householdCount: 1, initialCapacity: 1);

        for (var n = 0; n < 5; n++)
        {
            book.Add(Loan.Originate(0, 3, Money.FromEuros(100), EightPerCent, term: 12));
        }

        Assert.Equal(5, book.LiveCount);

        // €108 over twelve is €9.00 a month, except that the first instalment carries the odd
        // cents of both parts: 8.34 + 0.67.
        Assert.Equal(new Money(9_01) * 5, book.DebtService(0));
    }

    /// <summary>A retired loan cannot be added, and a term under one month is not a loan.</summary>
    [Fact]
    public void ARetiredLoanIsRefused()
    {
        var book = new LoanBook(1, 1);
        var loan = Loan.Originate(0, 3, Money.FromEuros(120), EightPerCent, term: 1).AfterPayment();

        Assert.True(loan.IsRetired);
        Assert.Throws<ArgumentException>(() => book.Add(loan));
        Assert.Throws<ArgumentOutOfRangeException>(() => Loan.Originate(0, 3, Money.FromEuros(120), EightPerCent, term: 0));
    }
}
