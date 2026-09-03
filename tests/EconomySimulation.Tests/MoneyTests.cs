using System.Globalization;
using EconomySimulation.Engine;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/01-02.</summary>
public sealed class MoneyTests
{
    [Fact]
    public void CompileFail_AssigningADoubleToMoney_IsCS0029()
    {
        CompileFail.Produces("CS0029", CompileFail.InMethod("        Money m = 1234.56;"));
    }

    [Fact]
    public void CompileFail_ConstructingMoneyFromADouble_DoesNotBuild()
    {
        CompileFail.Produces("CS1503", CompileFail.InMethod("        var m = new Money(1234.56);"));
    }

    [Fact]
    public void CompileFail_AssigningADecimalToMoney_IsCS0029()
    {
        CompileFail.Produces("CS0029", CompileFail.InMethod("        Money m = 1234.56m;"));
    }

    [Fact]
    public void ConstructingFromCents_Compiles()
    {
        CompileFail.Compiles(CompileFail.InMethod("        _ = new Money(123456L);"));
    }

    // ---- splitting ----------------------------------------------------------------------

    /// <summary>
    /// The awkward value the story names. Three parts of 33 with a cent left behind is the
    /// classic way a conservation invariant starts failing once a week.
    /// </summary>
    [Fact]
    public void Split_OneEuroInThree_Is34_33_33()
    {
        var parts = Money.FromEuros(1).Split(3);

        Assert.Equal([34L, 33L, 33L], parts.Select(p => p.Cents));
    }

    [Theory]
    [InlineData(100, 3)]
    [InlineData(1, 7)]
    [InlineData(0, 5)]
    [InlineData(99999, 13)]
    [InlineData(-100, 3)]
    [InlineData(-1, 7)]
    [InlineData(65000, 1000)]
    [InlineData(7, 11)]
    public void Split_PartsAlwaysSumToTheOriginal(long cents, int parts)
    {
        var pieces = new Money(cents).Split(parts);

        Assert.Equal(parts, pieces.Length);
        Assert.Equal(cents, pieces.Sum(p => p.Cents));
    }

    [Fact]
    public void Split_PartsDifferByAtMostOneCent()
    {
        foreach (var cents in Enumerable.Range(0, 500).Select(i => (long)i * 7 - 1000))
        {
            var pieces = new Money(cents).Split(7).Select(p => p.Cents).ToArray();

            Assert.True(
                pieces.Max() - pieces.Min() <= 1,
                $"{cents} split into 7 gave a spread of {pieces.Max() - pieces.Min()}");
        }
    }

    // ---- allocating in proportion -------------------------------------------------------

    /// <summary>
    /// The tier increments: budget is 0.60 of the standard price and the upgrade is the rest.
    /// Rounding the two independently is off by a cent on the prices that happen to be awkward,
    /// and this is what makes them add up instead.
    /// </summary>
    [Fact]
    public void Allocate_TierIncrementsAlwaysSumToTheStandardPrice()
    {
        for (var cents = 1L; cents <= 5000; cents++)
        {
            var parts = new Money(cents).Allocate([0.60, 0.40]);

            Assert.Equal(cents, parts[0].Cents + parts[1].Cents);
        }
    }

    [Fact]
    public void Allocate_GivesTheLeftoverToTheLargestDiscardedFraction()
    {
        // 10 cents in the proportions 1 : 1 : 1 is 3.33 each; the spare cent goes to the first.
        var parts = new Money(10).Allocate([1.0, 1.0, 1.0]);

        Assert.Equal([4L, 3L, 3L], parts.Select(p => p.Cents));
    }

    [Fact]
    public void Allocate_HandlesAZeroWeightWithoutGivingItAnything()
    {
        var parts = new Money(101).Allocate([1.0, 0.0]);

        Assert.Equal([101L, 0L], parts.Select(p => p.Cents));
    }

    [Fact]
    public void Allocate_NegativeTotalsStillSumExactly()
    {
        var parts = new Money(-101).Allocate([0.6, 0.4]);

        Assert.Equal(-101L, parts.Sum(p => p.Cents));
    }

    // ---- scaling ------------------------------------------------------------------------

    [Theory]
    [InlineData(30000, 0.60, 18000)]
    [InlineData(30000, 1.80, 54000)]
    [InlineData(100, 0.005, 1)]     // half a cent, away from zero
    [InlineData(-100, 0.005, -1)]   // and away from zero on the other side too
    [InlineData(12345, 1.0, 12345)]
    public void Scaled_RoundsHalvesAwayFromZero(long cents, double ratio, long expected)
    {
        Assert.Equal(expected, new Money(cents).Scaled(ratio).Cents);
    }

    [Fact]
    public void Scaled_RejectsNaNRatherThanProducingAPlausibleBalance()
    {
        Assert.Throws<OverflowException>(() => new Money(100).Scaled(double.NaN));
    }

    [Fact]
    public void Scaled_RejectsAnOverflowingRatio()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue / 2).Scaled(1e9));
    }

    // ---- arithmetic ---------------------------------------------------------------------

    [Fact]
    public void Addition_Overflows_Loudly()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue) + new Money(1));
    }

    [Fact]
    public void Multiplication_Overflows_Loudly()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue) * 2);
    }

    [Fact]
    public void Arithmetic_Behaves()
    {
        Assert.Equal(new Money(300), new Money(100) + new Money(200));
        Assert.Equal(new Money(-100), new Money(100) - new Money(200));
        Assert.Equal(new Money(-100), -new Money(100));
        Assert.Equal(new Money(600), new Money(200) * 3);
        Assert.Equal(new Money(600), 3 * new Money(200));
        Assert.True(new Money(1) > Money.Zero);
        Assert.True(new Money(-1) < Money.Zero);
        Assert.True(Money.Zero.IsZero);
        Assert.True(new Money(-1).IsNegative);
    }

    // ---- formatting ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(5, "0.05")]
    [InlineData(100, "1.00")]
    [InlineData(123456, "1234.56")]
    [InlineData(-123456, "-1234.56")]
    [InlineData(65000000, "650000.00")]
    public void ToCsv_IsFixedToTwoDecimalsWithNoSeparators(long cents, string expected)
    {
        Assert.Equal(expected, new Money(cents).ToCsv());
    }

    /// <summary>
    /// The failure this guards against does not appear on the machine that writes the code. It
    /// appears when the run is repeated somewhere with a comma decimal separator, and then V2
    /// is measuring the locale rather than the model.
    /// </summary>
    [Fact]
    public void ToCsv_IgnoresTheAmbientCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("1234.56", new Money(123456).ToCsv());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ToString_IsNotTheCsvForm_SoAnAccidentalInterpolationIsVisible()
    {
        Assert.Equal("1234.56 EUR", new Money(123456).ToString());
    }
}
