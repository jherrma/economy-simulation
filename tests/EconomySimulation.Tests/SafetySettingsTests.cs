using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// The settings in Directory.Build.props, asserted rather than assumed. See spec/stories/01-01.
/// </summary>
public sealed class SafetySettingsTests
{
    /// <summary>
    /// A switch expression that does not cover every case is an error, not a warning — which is
    /// what makes adding a case to an enum a compile-time task rather than a runtime surprise.
    ///
    /// This only holds while the switch has no discard arm, so that is what is asserted.
    /// </summary>
    [Fact]
    public void CompileFail_SwitchExpressionMissingAnEnumCase_IsCS8509()
    {
        CompileFail.Produces(
            "CS8509",
            """
            internal enum Tier { Budget, Standard, Premium }

            internal static class Probe
            {
                internal static int Multiplier(Tier tier) => tier switch
                {
                    Tier.Budget => 60,
                    Tier.Standard => 100,
                };
            }
            """);
    }

    /// <summary>
    /// The same switch with a discard arm compiles. Without this, the test above would pass for
    /// any reason at all — including a typo in the snippet.
    /// </summary>
    [Fact]
    public void SwitchExpressionWithADiscardArm_Compiles()
    {
        CompileFail.Compiles(
            """
            internal enum Tier { Budget, Standard, Premium }

            internal static class Probe
            {
                internal static int Multiplier(Tier tier) => tier switch
                {
                    Tier.Budget => 60,
                    Tier.Standard => 100,
                    _ => 180,
                };
            }
            """);
    }

    /// <summary>
    /// CheckForOverflowUnderflow, at runtime. A wrapped <c>long</c> in a sum of balances would
    /// not crash: it would produce a money stock that is wrong by 2^64 and an economic result
    /// to go with it. This is the one guarantee that cannot be checked by compiling a snippet,
    /// because the snippet's own compilation options are set by this harness rather than by the
    /// build — so it is exercised in the built test assembly itself.
    /// </summary>
    [Fact]
    public void LongOverflow_Throws_RatherThanWrapping()
    {
        var large = long.MaxValue;

        Assert.Throws<OverflowException>(() =>
        {
            var wrapped = large + 1;
            return wrapped;
        });
    }

    [Fact]
    public void LongUnderflow_Throws_RatherThanWrapping()
    {
        var small = long.MinValue;

        Assert.Throws<OverflowException>(() =>
        {
            var wrapped = small - 1;
            return wrapped;
        });
    }
}
