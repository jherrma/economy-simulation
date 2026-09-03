using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// **V2** — the same seed produces the same run, whatever else is true.
///
/// Three assertions, in increasing order of strength (`spec/03-VERIFICATION.md` §V2):
///
/// 1. The same seed, run twice, is byte-identical.
/// 2. The same seed, run serially and across threads, is byte-identical. **Not "close"** — a
///    floating-point difference here means an accumulation is order-dependent, and an accumulation
///    that is order-dependent over 48 ticks will have drifted visibly over 360.
/// 3. Registering a new random purpose and never drawing from it changes nothing.
///
/// The third is the one that matters in six months. Every mechanism in `draft/` wants randomness,
/// and this is the assertion that says adding one did not silently move the model onto a different
/// random world. Without it no two versions of the simulation can be compared on the same seed, and
/// the paired-seed protocol every result in this project rests on is worthless.
///
/// It is a gate rather than a unit test because a unit test would compare two toy runs. What has to
/// be identical is the output the campaign collects, so that is what is compared.
/// </summary>
public static class DeterminismGate
{
    /// <summary>
    /// The purpose registered by the third check, named once so a second call to the gate in the
    /// same process does not try to register it twice — a duplicate is an error by design.
    /// </summary>
    public const string UnusedPurposeName = "gate_unused_stream";

    private static readonly Lock RegistrationLock = new();

    private static Purpose? unused;

    public static GateReport Run(SimulationParameters parameters, IReadOnlyList<int> seeds, Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(workspace);

        var report = new GateReport("V2 — determinism");
        var short_ = Runs.Short(parameters);

        // Every arm is a full campaign of `seeds`, written where a person can go and look at it.
        var first = workspace.Arm("first");
        var second = workspace.Arm("second");
        var threaded = workspace.Arm("threaded");

        if (!Ran(report, short_, seeds, first, threaded: false)
            || !Ran(report, short_, seeds, second, threaded: false)
            || !Ran(report, short_, seeds, threaded, threaded: true))
        {
            return report;
        }

        Identical(
            report.Check("the same seed, run twice"),
            first,
            second,
            seeds,
            Invariant($"{seeds.Count} seeds x {short_.Run.Ticks} ticks, run twice in sequence"));

        Identical(
            report.Check("the same seed, serially and across threads"),
            first,
            threaded,
            seeds,
            Invariant($"threaded arm ran on up to {Environment.ProcessorCount} processors"));

        // Only now: the arm this is compared against has to have been produced *before* the
        // purpose existed, or the check proves nothing.
        var purposesBefore = Purpose.All.Count;
        Register();

        var afterRegistration = workspace.Arm("after-registration");

        if (!Ran(report, short_, seeds, afterRegistration, threaded: false))
        {
            return report;
        }

        Identical(
            report.Check("a registered but undrawn purpose changes nothing"),
            first,
            afterRegistration,
            seeds,
            Invariant($"'{UnusedPurposeName}' registered between the two arms; {purposesBefore} purposes before, {Purpose.All.Count} after"));

        return report;
    }

    /// <summary>
    /// Adds the unused purpose, once per process. Idempotent because the registry is not:
    /// registering a name twice throws, which is the right behaviour for the model and the wrong
    /// one for a gate that may be run twice in a test session.
    /// </summary>
    private static void Register()
    {
        lock (RegistrationLock)
        {
            unused ??= Purpose.Register(UnusedPurposeName);
        }
    }

    private static bool Ran(
        GateReport report,
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        string into,
        bool threaded)
    {
        var run = Runs.Execute(parameters, seeds, into, threaded);

        if (run.IsFailed)
        {
            var check = report.Check(Invariant($"the runs into {Path.GetFileName(into)} complete"));

            foreach (var error in run.Errors)
            {
                check.Fail(error.Message);
            }

            return false;
        }

        return true;
    }

    private static void Identical(
        GateCheck check,
        string left,
        string right,
        IReadOnlyList<int> seeds,
        string observation)
    {
        check.Observe(observation);

        var difference = Comparison.FirstDifference(left, right, seeds, Comparison.OutputAndConfiguration);

        if (difference is not null)
        {
            check.Fail(difference.ToString());
            check.Fail(Invariant($"the two runs are in {left} and {right}; re-run with --keep to inspect them"));
        }
    }
}
