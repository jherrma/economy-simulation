using System.Text.RegularExpressions;
using EconomySimulation.Engine;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/01-03.</summary>
public sealed partial class RateTests
{
    private static readonly Rate LoanRate = new(8.0);

    /// <summary>
    /// The two values the specification quotes. If either of these moves, every credit result in
    /// the project moves with it, and nothing else in the model would notice.
    /// </summary>
    [Fact]
    public void EightPerCentOverTwentyFourMonths_IsExactly_1_16()
    {
        Assert.Equal(1.16, LoanRate.FinanceMultiplier(24));
    }

    [Fact]
    public void EightPerCentOverTwelveMonths_IsExactly_1_08()
    {
        Assert.Equal(1.08, LoanRate.FinanceMultiplier(12));
    }

    [Fact]
    public void AZeroTerm_CostsNothing()
    {
        Assert.Equal(1.0, LoanRate.FinanceMultiplier(0));
    }

    [Fact]
    public void AZeroRate_CostsNothingOverAnyTerm()
    {
        Assert.Equal(1.0, Rate.Zero.FinanceMultiplier(24));
    }

    /// <summary>
    /// Simple interest, not compound: twice the term is exactly twice the interest. This is what
    /// separates the specified arithmetic from the plausible-looking alternative.
    /// </summary>
    [Fact]
    public void InterestIsSimple_NotCompound()
    {
        var twelve = LoanRate.FinanceMultiplier(12) - 1.0;
        var twentyFour = LoanRate.FinanceMultiplier(24) - 1.0;

        Assert.Equal(2.0 * twelve, twentyFour, 12);
    }

    /// <summary>
    /// The failure mode this type exists for: a rate quoted in per cent, divided by twelve, is a
    /// monthly rate of 0.667 rather than 0.00667 — a hundredfold error that produces a credit
    /// effect roughly the size of the one the project is trying to measure.
    /// </summary>
    [Fact]
    public void TheWrongDivisorWouldBeAHundredTimesTooLarge()
    {
        var correct = LoanRate.FinanceMultiplier(24) - 1.0;
        const double naive = 8.0 / 12.0 * 24.0;

        Assert.Equal(100.0, naive / correct, 9);
    }

    // ---- reaching money -----------------------------------------------------------------

    [Fact]
    public void TotalRepayable_RoundsThroughMoney()
    {
        Assert.Equal(Money.FromEuros(1160), LoanRate.TotalRepayable(Money.FromEuros(1000), 24));
        Assert.Equal(Money.FromEuros(1080), LoanRate.TotalRepayable(Money.FromEuros(1000), 12));
    }

    [Fact]
    public void InterestOn_IsTheDifference()
    {
        var principal = new Money(90000);

        Assert.Equal(
            LoanRate.TotalRepayable(principal, 24) - principal,
            LoanRate.InterestOn(principal, 24));
    }

    [Fact]
    public void CompileFail_AddingARateToMoney_IsCS0019()
    {
        CompileFail.Produces(
            "CS0019",
            CompileFail.InMethod("        var wrong = new Money(100) + new Rate(8.0);"));
    }

    [Fact]
    public void CompileFail_MultiplyingMoneyByARate_IsCS0019()
    {
        CompileFail.Produces(
            "CS0019",
            CompileFail.InMethod("        var wrong = new Money(100) * new Rate(8.0);"));
    }

    [Fact]
    public void CompileFail_UsingARateAsANumber_IsCS0029()
    {
        CompileFail.Produces(
            "CS0029",
            CompileFail.InMethod("        double wrong = new Rate(8.0);"));
    }

    // ---- the divisor lives in exactly one place -----------------------------------------

    /// <summary>
    /// 1200 is the whole of this type's reason to exist. Anywhere else in the engine it means
    /// someone has written the conversion out by hand, and the version they wrote out by hand is
    /// the one that will disagree with this one.
    /// </summary>
    [Fact]
    public void NoEngineFileOtherThanRate_MentionsTheDivisor()
    {
        var offenders = Repo
            .EngineSources()
            .Where(p => Path.GetFileName(p) != "Rate.cs")
            .Where(p => File.ReadAllText(p).Contains("1200", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "1200 belongs only in Rate.cs, and turned up in: "
            + string.Join(", ", offenders.Select(Path.GetFileName)));
    }

    /// <summary>
    /// The wider scan: any line in the engine that talks about a rate and also divides or
    /// multiplies by 12, 100 or 1200 is doing the conversion by hand.
    /// </summary>
    [Fact]
    public void NoEngineFileOtherThanRate_ConvertsARateByHand()
    {
        var offenders = new List<string>();

        foreach (var path in Repo.EngineSources())
        {
            if (Path.GetFileName(path) == "Rate.cs")
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (InterestContext().IsMatch(lines[i]) && BareDivisor().IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetFileName(path)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Interest arithmetic outside Rate: " + string.Join("; ", offenders));
    }

    [GeneratedRegex(@"(?i)\b(rate|interest|apr|annual|annum|percent)")]
    private static partial Regex InterestContext();

    [GeneratedRegex(@"[/*]\s*\(?\s*(12|100|1200)(\.0)?[mMdDfF]?\b")]
    private static partial Regex BareDivisor();
}
