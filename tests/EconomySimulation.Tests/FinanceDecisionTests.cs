using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;
using FluentResults;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/06-02. θ, the myopic residual test, and what credit actually changes.</summary>
public sealed class FinanceDecisionTests
{
    private const int Hobby = 3;
    private const int Electronics = 4;
    private const int Appliances = 5;
    private const int Budget = 0;
    private const int Standard = 1;

    /// <summary>
    /// Credit on, with the reservation price on money off so that λ is λ for everyone and the
    /// mechanism under test is the only thing that differs between two households.
    /// </summary>
    private static readonly SimulationParameters CreditOn = SimulationParameters.Default with
    {
        Credit = SimulationParameters.Default.Credit with { CreditEnabled = true },
        Decision = SimulationParameters.Default.Decision with { BufferMonths = 0.0 },
    };

    private sealed record Event(int Household, int Category, int Tier, Money DeltaPrice, WalkOutcome Outcome);

    private sealed class Town
    {
        public Town(SimulationParameters parameters, Money[] incomes, Money[] cash, double theta, params int[] wanted)
        {
            Parameters = parameters;
            Goods = new GoodsTable(parameters);
            Market = new Market(Goods);
            Population = Households.Specified(Goods.CategoryCount, incomes, incomes.Select(_ => 1.0).ToArray(), theta);
            Books = Ledger.Open(cash, Money.FromEuros(1_000_000));
            Loans = new LoanBook(Population, 8);
            Walker = new Walker(parameters, Goods, Market, Population, Books, Loans, runSeed: 1);

            for (var h = 0; h < incomes.Length; h++)
            {
                foreach (var c in wanted)
                {
                    // A durable is wanted when its unit has reached the end of its life.
                    Population.Age[Population.AgeIndex(h, c)] = Goods.Categories[c].Life;
                    Population.RefreshWant(h, c, Goods.Categories[c].Life);
                }
            }
        }

        public SimulationParameters Parameters { get; }

        public GoodsTable Goods { get; }

        public Market Market { get; }

        public Households Population { get; }

        public Ledger Books { get; }

        public LoanBook Loans { get; }

        public Walker Walker { get; }

        public List<Event> Walk()
        {
            var events = new List<Event>();
            Walker.Observer = (int h, in Candidate c, WalkOutcome o) => events.Add(new Event(h, c.Category, c.Tier, c.DeltaPrice, o));

            var result = Walker.Run(1);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");

            Walker.Observer = null;
            return events;
        }

        /// <summary>The tier the household holds in the category after the walk, −1 for none.</summary>
        public static int TierOf(List<Event> events, int household, int category) =>
            events.Where(e => e.Household == household && e.Category == category && e.Outcome is WalkOutcome.Taken or WalkOutcome.Financed)
                .Select(e => e.Tier)
                .DefaultIfEmpty(-1)
                .Max();
    }

    // ---- the sentence the write-up will make -------------------------------------------------

    /// <summary>
    /// Two identical households on €1000 wanting electronics, differing only in cash. Budget is
    /// €540 and the step to standard €360 more (score 1.40, financed 1.21); premium (0.88) does not
    /// clear λ for either. The household with €2000 pays cash for standard. The one with €600 pays
    /// cash for budget and has €60 left: with θ = 0 it stays on budget — a tier below, for no
    /// reason of preference — and with θ = 1 it finances the €360 step and lands on standard too.
    /// Which of those happens is what θ decides.
    /// </summary>
    [Theory]
    [InlineData(0.0, Budget)]
    [InlineData(1.0, Standard)]
    public void IdenticalHouseholds_DifferingOnlyInCash_LandWhereThetaSays(double theta, int expectedTierWithoutCash)
    {
        var town = new Town(
            CreditOn,
            [Money.FromEuros(1000), Money.FromEuros(1000)],
            [Money.FromEuros(2000), Money.FromEuros(600)],
            theta,
            Electronics);

        var events = town.Walk();

        Assert.Equal(Standard, Town.TierOf(events, 0, Electronics));
        Assert.Equal(expectedTierWithoutCash, Town.TierOf(events, 1, Electronics));

        // The household with cash never financed anything, whatever θ.
        Assert.DoesNotContain(events, e => e.Household == 0 && e.Outcome == WalkOutcome.Financed);
        Assert.Equal(0, town.Loans.LiveLoans(0));

        if (theta > 0)
        {
            var financed = Assert.Single(events, e => e.Household == 1 && e.Outcome == WalkOutcome.Financed);
            Assert.Equal(Standard, financed.Tier);

            // The loan is for the increment, not the tier price, and the increments and their loans line up.
            var loan = Assert.Single(town.Loans.LoansOf(1));
            Assert.Equal(Money.FromEuros(360), loan.Principal);
            Assert.Equal(financed.DeltaPrice, loan.Principal);
            Assert.Equal(24, loan.Term);
            Assert.Equal(Electronics, loan.Category);
        }
        else
        {
            Assert.Single(events, e => e.Household == 1 && e.Tier == Standard && e.Outcome == WalkOutcome.Unaffordable);
            Assert.Equal(0, town.Loans.LiveLoans(1));
        }

        // Either way, the household holds one unit at its final tier and has paid its posted price.
        Assert.Equal(2, town.Market.Sold(Electronics, Standard) + town.Market.Sold(Electronics, Budget));
    }

    /// <summary>Money side of the same walk: the financed increment appears as new money against a claim, and is spent into the pool.</summary>
    [Fact]
    public void AFinancedIncrement_CreatesExactlyThePrincipal_AndSpendsIt()
    {
        var town = new Town(CreditOn, [Money.FromEuros(1000)], [Money.FromEuros(600)], theta: 1.0, Electronics);
        var poolBefore = town.Books.Pool;

        town.Walk();

        Assert.Equal(Money.FromEuros(360), town.Books.LoansOutstanding);
        Assert.Equal(Money.FromEuros(360), town.Books.NetMoneyCreated);
        Assert.Equal(Money.FromEuros(60), town.Books.Cash(0));
        Assert.Equal(poolBefore + Money.FromEuros(900), town.Books.Pool);
        Assert.True(town.Books.Check(1, moneyCreation: true).IsSuccess);
    }

    // ---- the four conditions -----------------------------------------------------------------

    /// <summary>The finance branch is never reached by a household that can pay cash, however keen it is to borrow.</summary>
    [Fact]
    public void AHouseholdWithCash_NeverBorrows()
    {
        var town = new Town(CreditOn, [Money.FromEuros(650)], [Money.FromEuros(5000)], theta: 1.0, Hobby, Electronics, Appliances);

        var events = town.Walk();

        Assert.Contains(events, e => e.Outcome == WalkOutcome.Taken);
        Assert.DoesNotContain(events, e => e.Outcome == WalkOutcome.Financed);
        Assert.Equal(0, town.Loans.LiveCount);
    }

    /// <summary>With credit off, nothing is financed and no finance stream is ever drawn from — the walk is the 04-05 walk.</summary>
    [Fact]
    public void WithCreditOff_NothingIsFinanced()
    {
        var off = CreditOn with { Credit = CreditOn.Credit with { CreditEnabled = false } };
        var town = new Town(off, [Money.FromEuros(1000)], [Money.FromEuros(600)], theta: 1.0, Electronics);

        var events = town.Walk();

        Assert.Equal(Budget, Town.TierOf(events, 0, Electronics));
        Assert.Equal(0, town.Loans.LiveCount);
    }

    /// <summary>
    /// The financed score must clear λ on its own. On €700 the electronics step to standard clears
    /// λ in cash but not once divided by 1.16 — the preconditions are asserted from the ladder, not
    /// assumed — so a household with no cash finances budget and stops there. Credit widens the
    /// choice set; it never makes a step look cheaper than it is.
    /// </summary>
    [Fact]
    public void TheFinancedScoreMustClearLambda()
    {
        var town = new Town(CreditOn, [Money.FromEuros(700)], [Money.Zero], theta: 1.0, Electronics);

        var ladder = new Candidate[3];
        Ladder.Build(town.Goods, town.Market, town.Population, 0, Electronics, ladder);
        var multiplier = CreditOn.Credit.LoanRate.FinanceMultiplier(24);
        Assert.True(ladder[Standard].Score >= CreditOn.Decision.Lambda, $"cash score {ladder[Standard].Score}");
        Assert.True(ladder[Standard].Score / multiplier < CreditOn.Decision.Lambda, $"financed score {ladder[Standard].Score / multiplier}");

        var events = town.Walk();

        Assert.Equal(Budget, Town.TierOf(events, 0, Electronics));
        var loan = Assert.Single(town.Loans.LoansOf(0));
        Assert.Equal(Money.FromEuros(540), loan.Principal);
        Assert.Single(events, e => e.Tier == Standard && e.Outcome == WalkOutcome.Unaffordable);
    }

    /// <summary>
    /// Stacking, bounded by the residual. A household on €650 with no cash and θ = 1 wants all
    /// three financeable durables: it finances budget in each — no standard step clears λ at that
    /// income — three loans on three categories, €81.70 a month against a residual of €292.50.
    /// Raise the subsistence share to 0.90 and the residual is €65: electronics (€26.10) and
    /// appliances (€23.20) fit, hobby (€32.40) no longer does, and the walk stops at two loans.
    /// Debt service never exceeds the residual, because each origination counts the loans before it.
    /// </summary>
    [Theory]
    [InlineData(0.55, 3, 81_70)]
    [InlineData(0.90, 2, 49_30)]
    public void LoansStackAcrossCategories_BoundedByTheResidual(double subsistenceShare, int expectedLoans, long expectedDebtServiceCents)
    {
        var parameters = CreditOn with { Decision = CreditOn.Decision with { SubsistenceShare = subsistenceShare } };
        var town = new Town(parameters, [Money.FromEuros(650)], [Money.Zero], theta: 1.0, Hobby, Electronics, Appliances);

        town.Walk();

        Assert.Equal(expectedLoans, town.Loans.LiveLoans(0));
        Assert.Equal(new Money(expectedDebtServiceCents), town.Loans.DebtService(0));

        var residual = Money.FromEuros(650) - Money.FromEuros(650).Scaled(subsistenceShare);
        Assert.True(town.Loans.DebtService(0) <= residual);
    }

    /// <summary>
    /// The horizon is one tick. Under `full_term` the whole repayable amount must fit the residual
    /// — €626.40 for a budget appliance against €292.50 — so the same household finances nothing.
    /// </summary>
    [Fact]
    public void UnderTheFullTermHorizon_TheSameHouseholdFinancesNothing()
    {
        var control = CreditOn with { Decision = CreditOn.Decision with { AffordabilityHorizon = AffordabilityHorizon.FullTerm } };
        var town = new Town(control, [Money.FromEuros(650)], [Money.Zero], theta: 1.0, Hobby, Electronics, Appliances);

        var events = town.Walk();

        Assert.Equal(0, town.Loans.LiveCount);
        Assert.All(events, e => Assert.Equal(WalkOutcome.Unaffordable, e.Outcome));
    }

    /// <summary>
    /// And in a whole town: `credit_high` for two years, myopic against `full_term`. Under the
    /// control, loan stacking all but disappears — the households carrying two or more loans are a
    /// small fraction of the myopic count.
    /// </summary>
    [Fact]
    public void UnderTheFullTermHorizon_StackingAllButDisappears()
    {
        var high = SimulationParameters.Default with
        {
            Credit = SimulationParameters.Default.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 },
        };
        var control = high with { Decision = high.Decision with { AffordabilityHorizon = AffordabilityHorizon.FullTerm } };

        var stackedMyopic = StackedHouseholdsAfter(high, ticks: 24);
        var stackedFullTerm = StackedHouseholdsAfter(control, ticks: 24);

        Assert.True(stackedMyopic >= 20, $"myopic: {stackedMyopic} households carry two or more loans");
        Assert.True(stackedFullTerm * 10 <= stackedMyopic, $"full_term: {stackedFullTerm} against {stackedMyopic} myopic");
    }

    private static int StackedHouseholdsAfter(SimulationParameters parameters, int ticks)
    {
        var simulation = new Simulation(parameters, runSeed: 1);
        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= ticks; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");
        }

        return Enumerable.Range(0, simulation.Population.Count).Count(h => simulation.Loans.LiveLoans(h) >= 2);
    }

    /// <summary>
    /// θ = 0 for everyone is the baseline exactly: credit switched on with nobody willing to borrow
    /// leaves every price and every balance where credit off leaves them. The draws that were made
    /// changed nothing, which is the first half of V5 (08-04).
    /// </summary>
    [Fact]
    public void CreditOnWithThetaZero_IsTheBaseline()
    {
        var off = SimulationParameters.Default;
        var on = off with { Credit = off.Credit with { CreditEnabled = true, ThetaMin = 0.0, ThetaMax = 0.0 } };

        var a = new Simulation(off, runSeed: 2);
        var b = new Simulation(on, runSeed: 2);
        Assert.True(a.Start().IsSuccess);
        Assert.True(b.Start().IsSuccess);

        for (var tick = 1; tick <= 12; tick++)
        {
            Assert.True(a.RunTick(tick).IsSuccess);
            Assert.True(b.RunTick(tick).IsSuccess);
        }

        Assert.Equal(0, b.Loans.LiveCount);
        Assert.Equal(a.Books.Pool, b.Books.Pool);

        for (var h = 0; h < a.Population.Count; h++)
        {
            Assert.Equal(a.Books.Cash(h), b.Books.Cash(h));
        }

        for (var c = 0; c < a.Goods.CategoryCount; c++)
        {
            for (var t = 0; t < a.Goods.TierCount; t++)
            {
                Assert.Equal(a.Market.Price(c, t), b.Market.Price(c, t));
            }
        }
    }

    /// <summary>A whole credit_high tick still allocates nothing, loans and all.</summary>
    [Fact]
    public void ACreditHighTickAllocatesNothing()
    {
        var high = SimulationParameters.Default with
        {
            Credit = SimulationParameters.Default.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 },
        };
        var simulation = new Simulation(high, runSeed: 1);
        Assert.True(simulation.Start().IsSuccess);

        var tick = 0;
        Result? measured = null;

        // The warm-up also has to reach every path with loans live: origination, service, and the
        // retirement of a twelve-month loan.
        var allocated = Infrastructure.Allocations.Of(
            () => Assert.True(Results.IsOk(simulation.RunTick(++tick))),
            () => measured = simulation.RunTick(++tick));

        Assert.Equal(0, allocated);
        Assert.True(Results.IsOk(measured!));
        Assert.True(simulation.Loans.LiveCount > 0);
    }
}
