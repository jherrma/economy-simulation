namespace EconomySimulation.Tests.Infrastructure;

/// <summary>
/// Measuring that a piece of the tick allocates nothing.
///
/// The warm-up is the whole subtlety. .NET's tiered compilation promotes a method to optimised
/// code after its thirtieth call, and a promotion landing inside the measured window shows up as
/// several kilobytes that belong to the runtime rather than to the model — which made two of these
/// tests fail perhaps one run in five, and only when the whole suite ran. Warming up past the
/// promotion threshold is what makes the measurement about the code under test.
/// </summary>
internal static class Allocations
{
    /// <summary>Calls per method needed before tiered compilation has finished with it, with room to spare.</summary>
    internal const int WarmupCalls = 40;

    /// <summary>Bytes allocated by <paramref name="measured"/>, after <paramref name="warmup"/> has been run enough times.</summary>
    internal static long Of(Action warmup, Action measured)
    {
        ArgumentNullException.ThrowIfNull(warmup);
        ArgumentNullException.ThrowIfNull(measured);

        for (var i = 0; i < WarmupCalls; i++)
        {
            warmup();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        measured();

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
