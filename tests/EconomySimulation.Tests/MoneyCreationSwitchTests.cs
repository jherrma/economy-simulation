using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Credit;
using EconomySimulation.Engine.Decision;
using EconomySimulation.Engine.Ledger;
using EconomySimulation.Engine.World;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/06-04. The cheapest interesting experiment in the model.</summary>
public sealed class MoneyCreationSwitchTests
{
    private const int Electronics = 4;

    private static readonly SimulationParameters CreditHigh = SimulationParameters.Default with
    {
        Credit = SimulationParameters.Default.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 },
    };

    private static readonly SimulationParameters CreditHighNoCreation = CreditHigh with
    {
        Credit = CreditHigh.Credit with { MoneyCreation = false },
    };

    private static Simulation Run(SimulationParameters parameters, int seed, int ticks, Action<Simulation, int>? afterTick = null)
    {
        var simulation = new Simulation(parameters, seed);
        Assert.True(simulation.Start().IsSuccess);

        for (var tick = 1; tick <= ticks; tick++)
        {
            var result = simulation.RunTick(tick);
            Assert.True(result.IsSuccess, result.IsFailed ? result.Errors[0].Message : "");
            afterTick?.Invoke(simulation, tick);
        }

        return simulation;
    }

    /// <summary>
    /// With creation off the money stock is constant: `Σ cash + pool == M0` at every tick while
    /// loans are outstanding, because a loan is a claim on money that already exists. Nothing was
    /// created, so `net_money_created` stays zero, and the check's second assertion is what
    /// polices that at this setting.
    /// </summary>
    [Fact]
    public void WithCreationOff_TheMoneyStockIsConstant()
    {
        var sawLoans = false;

        Run(CreditHighNoCreation, seed: 1, ticks: 36, (simulation, _) =>
        {
            var books = simulation.Books;

            Assert.Equal(books.M0, books.MoneyHeld);
            Assert.Equal(Money.Zero, books.NetMoneyCreated);
            Assert.Equal(Money.Zero, books.Created);

            sawLoans |= books.LoansOutstanding > Money.Zero;
        });

        Assert.True(sawLoans, "no loan was ever outstanding, so the setting was not exercised");
    }

    /// <summary>With creation on, the same run's money stock moves with the loans, one for one.</summary>
    [Fact]
    public void WithCreationOn_TheMoneyStockMovesWithTheLoans()
    {
        var sawLoans = false;

        Run(CreditHigh, seed: 1, ticks: 36, (simulation, _) =>
        {
            var books = simulation.Books;

            Assert.Equal(books.M0 + books.LoansOutstanding, books.MoneyHeld);
            Assert.Equal(books.LoansOutstanding, books.NetMoneyCreated);

            sawLoans |= books.LoansOutstanding > Money.Zero;
        });

        Assert.True(sawLoans);
    }

    /// <summary>
    /// Only the funding differs. In the first tick the two settings see the same prices, the same
    /// order and the same cash — a borrower's cash is the same after an origination and a purchase
    /// whichever way the principal arrived — so the loans, the households' balances and the prices
    /// set for tick 2 are identical, and the pool differs by exactly the principal lent.
    /// </summary>
    [Fact]
    public void OnlyTheFundingDiffers()
    {
        var on = Run(CreditHigh, seed: 3, ticks: 1);
        var off = Run(CreditHighNoCreation, seed: 3, ticks: 1);

        Assert.True(on.Loans.LiveCount > 0);
        Assert.Equal(on.Loans.LiveCount, off.Loans.LiveCount);
        Assert.Equal(on.Books.LoansOutstanding, off.Books.LoansOutstanding);
        Assert.Equal(0, off.Shopping.Rationed);

        for (var h = 0; h < on.Population.Count; h++)
        {
            Assert.Equal(on.Books.Cash(h), off.Books.Cash(h));
            Assert.Equal(on.Loans.LoansOf(h), off.Loans.LoansOf(h));
        }

        for (var c = 0; c < on.Goods.CategoryCount; c++)
        {
            for (var t = 0; t < on.Goods.TierCount; t++)
            {
                Assert.Equal(on.Market.Price(c, t), off.Market.Price(c, t));
                Assert.Equal(on.Market.Sold(c, t), off.Market.Sold(c, t));
            }
        }

        Assert.Equal(on.Books.Pool - on.Books.LoansOutstanding, off.Books.Pool);
    }

    /// <summary>
    /// A pool that cannot cover the principal rations the loan: recorded against the tier as
    /// unaffordable and counted, never halted. Two households on €1000 with no cash want
    /// electronics; the pool holds €400 against a €540 budget unit, so both are rationed at budget,
    /// nobody borrows, and the walk completes with the money stock untouched.
    ///
    /// The pool has to start below one principal for this to happen at all: the principal a loan
    /// draws out is spent straight back in by the same purchase, so the pool is only ever short by
    /// the amount of the loan being made. In a default run rationing therefore never occurs — see
    /// `01-SIMULATION.md` §7.3.
    /// </summary>
    [Fact]
    public void APoolThatCannotFundTheLoan_RationsIt_AndTheRunContinues()
    {
        var parameters = CreditHighNoCreation with
        {
            Decision = CreditHighNoCreation.Decision with { BufferMonths = 0.0 },
        };
        var goods = new GoodsTable(parameters);
        var market = new Market(goods);
        var population = Households.Specified(goods.CategoryCount, [Money.FromEuros(1000), Money.FromEuros(1000)], [1.0, 1.0], theta: 1.0);
        var books = Ledger.Open([Money.Zero, Money.Zero], Money.FromEuros(400));
        var loans = new LoanBook(population, 4);
        var walker = new Walker(parameters, goods, market, population, books, loans, runSeed: 1);

        for (var h = 0; h < 2; h++)
        {
            population.Age[population.AgeIndex(h, Electronics)] = goods.Categories[Electronics].Life;
            population.RefreshWant(h, Electronics, goods.Categories[Electronics].Life);
        }

        var outcomes = new List<(int Household, int Tier, WalkOutcome Outcome)>();
        walker.Observer = (int h, in Candidate c, WalkOutcome o) => outcomes.Add((h, c.Tier, o));

        var result = walker.Run(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, o => Assert.Equal((0, WalkOutcome.Rationed), (o.Tier, o.Outcome)));
        Assert.Equal(2, walker.Rationed);
        Assert.Equal(2, market.Unaffordable(Electronics, 0));
        Assert.Equal(0, loans.LiveCount);
        Assert.Equal(Money.FromEuros(400), books.Pool);
        Assert.True(books.Check(1, moneyCreation: false).IsSuccess);
        Assert.Equal(books.M0, books.MoneyHeld);

        // And with €600 the first household in the order is funded for budget and, the pool having
        // been refilled by that very purchase, for the step to standard as well; the second finds
        // €600 again and is funded too. Rationing is a property of the pool's momentary balance.
        var richer = Ledger.Open([Money.Zero, Money.Zero], Money.FromEuros(600));
        var richerLoans = new LoanBook(population, 4);
        var richerWalker = new Walker(parameters, goods, new Market(goods), population, richer, richerLoans, runSeed: 1);

        for (var h = 0; h < 2; h++)
        {
            population.RefreshWant(h, Electronics, goods.Categories[Electronics].Life);
        }

        Assert.True(richerWalker.Run(1).IsSuccess);
        Assert.Equal(0, richerWalker.Rationed);
        Assert.Equal(4, richerLoans.LiveCount);
        Assert.Equal(Money.FromEuros(600), richer.Pool);
    }

    /// <summary>
    /// The finding this story produced (`01-SIMULATION.md` §7.3): `credit_high` at both settings on
    /// the same seed is **identical in every quantity a household can see** — every price, every
    /// balance, every loan — for the whole run. The story expected the results to differ; they
    /// cannot. A borrower's cash is the same whether the principal was created or drawn from the
    /// pool, the pool pays the same fixed incomes either way, and the pool has no behaviour of its
    /// own, so the entire difference between the settings sits in the pool's balance and in the
    /// money stock, one for one with the loans outstanding. The money-creation channel is zero by
    /// construction in v1, and the switch's one observable effect — rationing — needs a pool below
    /// a single principal, which a default run never approaches.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CreditHighAtBothSettings_IsIdenticalToTheHousehold_AndDiffersOnlyInThePool(int seed)
    {
        var on = Run(CreditHigh, seed, ticks: 60);
        var off = Run(CreditHighNoCreation, seed, ticks: 60);

        Assert.True(on.Loans.LiveCount > 0);
        Assert.True(on.Books.LoansOutstanding > Money.Zero);

        for (var c = 0; c < on.Goods.CategoryCount; c++)
        {
            for (var t = 0; t < on.Goods.TierCount; t++)
            {
                Assert.Equal(on.Market.Price(c, t), off.Market.Price(c, t));
            }
        }

        for (var h = 0; h < on.Population.Count; h++)
        {
            Assert.Equal(on.Books.Cash(h), off.Books.Cash(h));
            Assert.Equal(on.Loans.LoansOf(h), off.Loans.LoansOf(h));
        }

        Assert.Equal(on.Books.LoansOutstanding, off.Books.LoansOutstanding);
        Assert.Equal(on.Books.Pool - on.Books.LoansOutstanding, off.Books.Pool);
        Assert.Equal(on.Books.MoneyHeld - on.Books.LoansOutstanding, off.Books.MoneyHeld);
        Assert.Equal(off.Books.M0, off.Books.MoneyHeld);
        Assert.Equal(0, off.Shopping.Rationed);
    }

    /// <summary>With creation on, nothing is ever rationed: the loan is made, not found.</summary>
    [Fact]
    public void WithCreationOn_NothingIsRationed()
    {
        var total = 0;

        Run(CreditHigh, seed: 1, ticks: 24, (simulation, _) => total += simulation.Shopping.Rationed);

        Assert.Equal(0, total);
    }
}
