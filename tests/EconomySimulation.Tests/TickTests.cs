using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>See spec/stories/03-04.</summary>
public sealed class TickTests
{
    private static readonly SimulationParameters Defaults = SimulationParameters.Default;

    // ---- the order --------------------------------------------------------------------------

    /// <summary>
    /// The step list against the committed fixture. Adding or reordering a step is then a
    /// deliberate edit to `spec/tick-order.txt`, with a reason attached, rather than a line moved
    /// in a method.
    /// </summary>
    [Fact]
    public void TheStepOrderMatchesTheCommittedFixture()
    {
        var fixture = File
            .ReadAllLines(Path.Combine(Repo.Root, "spec", "tick-order.txt"))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();

        var actual = Simulation.StepOrder.Select(Spelling).ToArray();

        Assert.Equal(fixture, actual);
    }

    [Fact]
    public void ThereAreSevenSteps()
    {
        Assert.Equal(7, Simulation.StepOrder.Count);
        Assert.Equal(Simulation.StepOrder.Count, Simulation.StepOrder.Distinct().Count());
    }

    /// <summary>
    /// And the tick actually runs them in that order, which is a different claim from the list
    /// being in that order.
    /// </summary>
    [Fact]
    public void ATickRunsEveryStepOnceInOrder()
    {
        var simulation = new Simulation(Defaults, runSeed: 3);
        var ran = new List<TickStep>();
        simulation.StepObserver = ran.Add;

        Assert.True(simulation.RunTick(1).IsSuccess);

        Assert.Equal(Simulation.StepOrder, ran);
    }

    // ---- what the order buys ------------------------------------------------------------------

    /// <summary>
    /// Debt service before the walk is what makes the affordability test at origination
    /// sufficient: the instalment is guaranteed to fit because it came out of income before
    /// anything else could claim it. Move it after and households spend their way into arrears,
    /// which v1 has no machinery to handle.
    /// </summary>
    [Fact]
    public void DebtServiceRunsBeforeTheWalk()
    {
        Assert.True(Position(TickStep.DebtService) < Position(TickStep.Walk));
        Assert.True(Position(TickStep.Income) < Position(TickStep.DebtService));
    }

    /// <summary>Ageing after the walk, so a unit bought this tick starts at 0 and is not wanted again.</summary>
    [Fact]
    public void AgeingRunsAfterTheWalk()
    {
        Assert.True(Position(TickStep.Walk) < Position(TickStep.Ageing));
        Assert.True(Position(TickStep.Wants) < Position(TickStep.Walk));
    }

    /// <summary>
    /// Repricing after the walk, on this tick's demand, applying from the next tick.
    ///
    /// The walk visits households in sequence. If a stockout moved a price mid-walk, the household
    /// visited first would face a different price from the one visited last, and the rationing
    /// order would silently become a price advantage.
    /// </summary>
    [Fact]
    public void RepricingRunsAfterTheWalk_AndTheCheckIsLast()
    {
        Assert.True(Position(TickStep.Walk) < Position(TickStep.Repricing));
        Assert.Equal(Simulation.StepOrder.Count - 1, Position(TickStep.Check));
    }

    /// <summary>
    /// Prices do not move during a tick's walk. Asserted by watching them at every step: whatever
    /// the walk does later, the price a household faces is fixed for the whole tick.
    /// </summary>
    [Fact]
    public void PricesDoNotChangeMidTick()
    {
        var simulation = new Simulation(Defaults, runSeed: 3);
        var opening = Prices(simulation);

        simulation.StepObserver = step =>
        {
            if (step is TickStep.Repricing or TickStep.Check)
            {
                return;
            }

            Assert.Equal(opening, Prices(simulation));
        };

        Assert.True(simulation.RunTick(1).IsSuccess);
    }

    // ---- the empty run ------------------------------------------------------------------------

    /// <summary>
    /// 360 empty ticks. Not a formality: this is what catches anything that accumulates when it
    /// should not, and it runs in milliseconds.
    /// </summary>
    [Fact]
    public void ThreeHundredAndSixtyEmptyTicks_LeaveTheTownExactlyAsItStarted()
    {
        var simulation = new Simulation(Defaults, runSeed: 3);

        var openingCash = Enumerable.Range(0, simulation.Population.Count).Select(simulation.Books.Cash).ToArray();
        var openingPool = simulation.Books.Pool;
        var openingPrices = Prices(simulation);
        var openingAges = simulation.Population.Age.ToArray();

        var run = simulation.Run();

        Assert.True(run.IsSuccess, run.IsFailed ? run.Errors[0].Message : "");
        Assert.Equal(360, simulation.Tick);

        Assert.Equal(openingCash, Enumerable.Range(0, simulation.Population.Count).Select(simulation.Books.Cash));
        Assert.Equal(openingPool, simulation.Books.Pool);
        Assert.Equal(openingPrices, Prices(simulation));
        Assert.Equal(openingAges, simulation.Population.Age);
        Assert.Equal(simulation.Books.M0, simulation.Books.MoneyHeld);
    }

    [Fact]
    public void AFailedCheckStopsTheRunWhereItHappened()
    {
        var simulation = new Simulation(Defaults, runSeed: 3);
        Assert.True(simulation.Start().IsSuccess);

        // Money from nowhere, with no claim behind it.
        Assert.True(
            simulation.Books
                .CreateMoney(Engine.Ledger.Account.Household(0), new Money(1), Engine.Ledger.TransferReason.LoanOrigination)
                .IsSuccess);

        var result = simulation.RunTick(1);

        Assert.True(result.IsFailed);
        Assert.Equal(-1, simulation.Tick);
    }

    // -------------------------------------------------------------------------------------------

    private static int Position(TickStep step) => Simulation.StepOrder.ToList().IndexOf(step);

    private static Money[] Prices(Simulation simulation) =>
        [.. Enumerable
            .Range(0, simulation.Goods.CategoryCount)
            .SelectMany(c => Enumerable.Range(0, simulation.Goods.TierCount).Select(t => simulation.Market.Price(c, t)))];

    private static string Spelling(TickStep step) => step switch
    {
        TickStep.Income => "income",
        TickStep.DebtService => "debt_service",
        TickStep.Wants => "wants",
        TickStep.Walk => "walk",
        TickStep.Ageing => "ageing",
        TickStep.Repricing => "repricing",
        TickStep.Check => "check",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "unspelled step"),
    };
}
