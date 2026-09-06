using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.World;
using EconomySimulation.Tests.Infrastructure;
using static System.FormattableString;

namespace EconomySimulation.Tests;

/// <summary>
/// See spec/stories/11-05. No shelf too thin to carry a price series.
///
/// A shelf is a (good, tier) pair and each one reprices on its own from its own excess demand. Below
/// a handful of units that series is not a measurement: one unit sold or not sold moves it by the
/// whole reprice step. The danger is not that it looks broken — it is that it looks exactly like
/// data, and somebody plots it.
///
/// Eighteen goods is what makes this pressing rather than theoretical. Splitting a category into
/// three divides its capacity three ways as well as its budget, and then the tier system divides it
/// again, so the thin shelves are wherever the lives are long.
/// </summary>
public sealed class ThinShelfTests
{
    private static string Path(string file) =>
        System.IO.Path.Combine(Repo.Root, "config", "calibrations", file);

    private static SimulationParameters Grouped()
    {
        var loaded = ConfigurationLoader.FromFile(Path("grouped.toml"));

        Assert.True(loaded.IsSuccess, string.Join("; ", loaded.Errors.Select(e => e.Message)));

        return loaded.Value;
    }

    private static IReadOnlyList<string> ThinAt(int households, int floor)
    {
        var toml = Invariant($"[run]\nhouseholds = {households}\nmin_shelf_units = {floor}\n");
        var loaded = ConfigurationLoader.FromToml(File.ReadAllText(Path("grouped.toml")) + toml);

        return loaded.IsSuccess ? [] : [.. loaded.Errors.Select(e => e.Message)];
    }

    // ---- the check itself ---------------------------------------------------------------------

    /// <summary>
    /// The floor is **off by default**, and that is a statement about §3.1 rather than a
    /// convenience.
    ///
    /// The v1 calibration at 1,000 households has an appliance premium shelf of **two** units and an
    /// electronics premium shelf of six. Turning the check on by default would reject the
    /// configuration every published v1 number came from, which is rewriting history rather than
    /// checking it. What it does mean is that §10.3's per-tier appliance series rests on a two-unit
    /// shelf, and §10 now says so.
    /// </summary>
    [Fact]
    public void TheFloorIsOffByDefault_AndTheV1CalibrationWouldNotClearIt()
    {
        Assert.Equal(0, SimulationParameters.Default.Run.MinShelfUnits);

        var loaded = ConfigurationLoader.FromToml("[run]\nmin_shelf_units = 7\n");

        Assert.True(loaded.IsFailed);

        var complaint = loaded.Errors[0].Message;

        Assert.Contains("appliances.premium = 2", complaint, StringComparison.Ordinal);
        Assert.Contains("electronics.premium = 6", complaint, StringComparison.Ordinal);

        // The two shelves either side of the appliance one, so the message is a list rather than a
        // single worst case: the fix is more households or a merged group, and neither follows from
        // one count.
        Assert.Contains("appliances.budget = 4", complaint, StringComparison.Ordinal);
        Assert.Contains("appliances.standard = 4", complaint, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The counts come from the largest-remainder allocation, not from `round(share × capacity)`.**
    ///
    /// The two disagree, and in the direction of passing: a capacity of 19 splits 8 / 7 / 4, where
    /// rounding each share on its own gives 8 / 8 / 4 — twenty units, one of which does not exist,
    /// and a middle shelf reported a unit thicker than it is. That is the laptop shelf at 1,000
    /// households, so it is not a hypothetical.
    /// </summary>
    [Fact]
    public void TheCountsAreTheAllocationsOwn_NotAPerTierRounding()
    {
        var shares = SimulationParameters.Default.Tiers.Select(t => t.UnitShare).ToArray();
        var allocated = Allocation.LargestRemainder(19, shares);

        Assert.Equal([8L, 7L, 4L], allocated);
        Assert.Equal(19L, allocated.Sum());

        // What rounding each share on its own would have said.
        var rounded = shares.Select(s => (long)Math.Round(19 * s, MidpointRounding.AwayFromZero)).ToArray();

        Assert.Equal([8L, 8L, 4L], rounded);
        Assert.Equal(20L, rounded.Sum());

        // And the goods table splits the way the check does, which is the point of using one
        // implementation for both.
        var parameters = Grouped().WithHouseholds(1000) with
        {
            Run = Grouped().Run with { Households = 1000, MinShelfUnits = 0 },
        };

        var goods = new GoodsTable(parameters);
        var laptop = Array.FindIndex([.. goods.Categories], c => c.Name == "laptop");

        Assert.Equal(19, goods.Categories[laptop].Capacity);
        Assert.Equal([8, 7, 4], [goods.Units(laptop, 0), goods.Units(laptop, 1), goods.Units(laptop, 2)]);
    }

    // ---- what it says about the grouped calibration -------------------------------------------

    /// <summary>
    /// At 1,000 households the eighteen-good table puts **four premium shelves at three units or
    /// fewer, one of them at one**. This is the evidence for §3.6's town, and it is why the check
    /// exists rather than the other way round.
    /// </summary>
    [Fact]
    public void TheGroupedCalibrationAtATousandHouseholds_HasShelvesOfOneUnit()
    {
        var parameters = Grouped().WithHouseholds(1000) with
        {
            Run = Grouped().Run with { Households = 1000, MinShelfUnits = 0 },
        };

        var goods = new GoodsTable(parameters);
        var premium = goods.TierCount - 1;

        var thin = Enumerable
            .Range(0, goods.CategoryCount)
            .Select(c => (Name: goods.Categories[c].Name, Units: goods.Units(c, premium)))
            .Where(shelf => shelf.Units <= 3)
            .ToArray();

        Assert.Equal(4, thin.Length);
        Assert.Equal(1, thin.Min(shelf => shelf.Units));
        Assert.Equal("appliance_large", thin.Single(shelf => shelf.Units == 1).Name);

        // A one-unit shelf is not a small sample, it is a coin toss with a price on it.
        Assert.Equal(
            ["hobby_big_kit", "tv", "appliance_medium", "appliance_large"],
            thin.Select(shelf => shelf.Name).ToArray());
    }

    /// <summary>
    /// **The floor forces the town, not the other way round.** 5,000 households clear a floor of
    /// seven; 4,000 do not, and the threshold is 4,680.
    ///
    /// Stated as a bracket rather than as an approval of 5,000, because the number that matters is
    /// where it stops working. The binding shelf is large appliances at exactly seven units, so
    /// §3.6's town has no margin on its narrowest shelf and a longer-lived good added later will
    /// need more households rather than a nudge to the floor.
    /// </summary>
    [Fact]
    public void FiveThousandHouseholdsIsWhatTheFloorForces()
    {
        Assert.Empty(ThinAt(5000, floor: 7));
        Assert.Empty(ThinAt(4680, floor: 7));

        Assert.NotEmpty(ThinAt(4679, floor: 7));
        Assert.NotEmpty(ThinAt(4000, floor: 7));
        Assert.NotEmpty(ThinAt(1000, floor: 7));

        // The binding shelf, named: no margin at all on the narrowest one.
        var goods = new GoodsTable(Grouped());
        var large = Array.FindIndex([.. goods.Categories], c => c.Name == "appliance_large");

        Assert.Equal(7, goods.Units(large, goods.TierCount - 1));
        Assert.Equal(
            7,
            Enumerable
                .Range(0, goods.CategoryCount)
                .SelectMany(c => Enumerable.Range(0, goods.TierCount).Select(t => goods.Units(c, t)))
                .Min());
    }

    /// <summary>
    /// The committed grouped calibration loads, states its own floor, and clears it.
    ///
    /// The floor lives in the calibration file rather than in a scenario because it is a fact about
    /// *this* goods table: eighteen goods split one capacity eighteen ways and then three ways
    /// again, and how thin that gets depends on the lives in the table.
    /// </summary>
    [Fact]
    public void TheCommittedGroupedCalibration_StatesItsFloorAndClearsIt()
    {
        var parameters = Grouped();

        Assert.Equal(5000, parameters.Run.Households);
        Assert.Equal(7, parameters.Run.MinShelfUnits);

        // And `capacity` is still nowhere in the file: it follows from households and life.
        foreach (var good in parameters.Categories)
        {
            Assert.Equal(
                SimulationParameters.DerivedCapacity(parameters.Run.Households, good.Life),
                good.Capacity);
        }
    }

    /// <summary>A negative floor is a mistake rather than a very permissive check.</summary>
    [Fact]
    public void ANegativeFloor_IsRejected()
    {
        var loaded = ConfigurationLoader.FromToml("[run]\nmin_shelf_units = -1\n");

        Assert.True(loaded.IsFailed);
        Assert.Contains(loaded.Errors, e => e.Message.Contains("run.min_shelf_units", StringComparison.Ordinal));
    }
}
