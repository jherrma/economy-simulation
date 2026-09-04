using System.Diagnostics;
using EconomySimulation.Engine.Configuration;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// **V5** — `credit_high` with every θ forced to zero reproduces `credit_off` byte for byte.
///
/// This does two jobs and the second is the more valuable.
///
/// As a **regression test** it says the credit machinery does not perturb the model when it is
/// switched off, which is where most mistakes at a mechanism boundary show up first: the economy is
/// perfectly capable of absorbing a wrong number into a plausible one. Combined with V2's
/// undrawn-purpose check it also says the θ draws themselves cost nothing.
///
/// As a **measurement instrument** it makes the on-versus-off difference the attributed effect of
/// exactly one mechanism, on paired seeds, with everything else held bit-identical. That is what
/// lets the write-up say how much of the result credit accounts for, and there is no honest way to
/// reconstruct it afterwards.
///
/// **The rule generalises, and this is where it is first used: a mechanism that cannot be switched
/// off is a mechanism whose contribution cannot be measured.** Every mechanism added after v1
/// arrives behind a switch whose *off* setting reproduces the previous version byte for byte on the
/// same seeds. If a future design does not admit an off switch, that is a reason to redesign it
/// rather than to skip the switch. Byte for byte and not "closely", because this model amplifies a
/// single cent into a different trajectory within a few dozen ticks (`01-SIMULATION.md` §10.2) —
/// "closely" is not something it can do.
/// </summary>
public static class CreditOffGate
{
    /// <summary>Where the committed baselines live.</summary>
    public static string BaselineDirectory => Path.Combine(WorkingTree.Baselines, "credit_off");

    /// <summary>What the baseline was made from, beside it. Read by a person, not by the gate.</summary>
    public const string ProvenanceFile = "provenance.toml";

    /// <summary>The configuration each arm is written under. Both arms, deliberately — see <see cref="Run"/>.</summary>
    private const string Label = "credit_off";

    /// <param name="baselines">
    /// Where the committed baselines are. Defaults to <see cref="BaselineDirectory"/>; the tests
    /// point it at a directory of their own, because what has to be tested here is what the gate
    /// does with a baseline that has gone stale, and there is no way to stage that in the one the
    /// working tree carries.
    /// </param>
    public static GateReport Run(
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        Workspace workspace,
        string? baselines = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(workspace);

        baselines ??= BaselineDirectory;

        var report = new GateReport("V5 — credit off reproduces the baseline");
        var (off, thetaZero) = Arms(parameters);

        Differ(report.Check("the two arms really are different configurations"), off, thetaZero);

        var offArm = workspace.Arm("credit_off");
        var zeroArm = workspace.Arm("credit_high-theta-zero");

        if (!Ran(report, off, seeds, offArm) || !Ran(report, thetaZero, seeds, zeroArm))
        {
            return report;
        }

        var identical = report.Check("credit_high with every theta = 0 is credit_off, byte for byte");
        var difference = Comparison.FirstDifference(offArm, zeroArm, seeds, Comparison.OutputFiles);

        identical.Observe(Invariant($"{seeds.Count} seeds x {off.Run.Ticks} ticks, run.csv and tiers.csv and the marker"));

        if (difference is not null)
        {
            identical.Fail(difference.ToString());
            identical.Fail(
                "The credit machinery is perturbing the model while switched off. Until this holds, the "
                + "difference between credit on and credit off is not the attributed effect of credit.");
        }

        AgainstBaselines(report, off, seeds, offArm, baselines);

        return report;
    }

    /// <summary>
    /// The two arms. Both are written under the label `credit_off`, and that is not the gate hiding
    /// a difference from itself.
    ///
    /// The scenario name is an input to the writer, not an output of the model: no part of the model
    /// reads it and setting it changes no draw (`02-PARAMETERS.md` §1). It exists so that the files
    /// of hundreds of runs concatenate without ambiguity. What this gate claims is precisely that
    /// these two configurations *are* the same run — so labelling one of them differently would make
    /// the comparison fail on the gate's own input rather than on the model's output, and there
    /// would then be a column that had to be excluded from a comparison that is otherwise total.
    /// The two configurations still differ, visibly, in the effective-config.toml written beside
    /// each run, and the check above asserts that they do.
    /// </summary>
    private static (SimulationParameters Off, SimulationParameters ThetaZero) Arms(SimulationParameters parameters)
    {
        var baseline = Runs.Short(parameters) with { Run = Runs.Short(parameters).Run with { Scenario = Label } };
        var high = Scenarios.CreditHigh(baseline);

        return (
            baseline with { Credit = baseline.Credit with { CreditEnabled = false } },
            high with
            {
                Run = high.Run with { Scenario = Label },
                Credit = high.Credit with { ThetaMin = 0.0, ThetaMax = 0.0 },
            });
    }

    private static void Differ(GateCheck check, SimulationParameters off, SimulationParameters thetaZero)
    {
        if (off == thetaZero)
        {
            check.Fail("the two arms are the same configuration; this gate would then be comparing a run with itself");

            return;
        }

        if (!thetaZero.Credit.CreditEnabled)
        {
            check.Fail("the treatment arm has credit_enabled = false; V5 is about credit switched on and never reached");
        }

        if (thetaZero.Credit.ThetaMin != 0.0 || thetaZero.Credit.ThetaMax != 0.0)
        {
            check.Fail(Invariant($"the treatment arm's theta is U({thetaZero.Credit.ThetaMin}, {thetaZero.Credit.ThetaMax}); V5 forces it to zero"));
        }

        check.Observe(Invariant(
            $"control: credit_enabled = false. treatment: credit_enabled = true, loan_rate = {thetaZero.Credit.LoanRate.PercentPerAnnum}, theta = 0"));
    }

    // ---- the committed baselines ---------------------------------------------------------------

    /// <summary>
    /// The same run against what was committed, so that a change which perturbs the model shows up
    /// even when both of today's arms move together.
    ///
    /// Small on purpose: 48 ticks and four seeds is a few hundred kilobytes, which is a size a
    /// working tree can carry and a reviewer can diff.
    /// </summary>
    private static void AgainstBaselines(
        GateReport report,
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        string arm,
        string baselines)
    {
        var check = report.Check("the run still matches the committed baselines");

        if (!Directory.Exists(baselines))
        {
            check.Fail(Invariant($"no baselines at {baselines}. Create them deliberately: dotnet run --project tools/Gates -- rebaseline"));

            return;
        }

        check.Observe(Provenance(baselines));

        foreach (var seed in seeds)
        {
            var stored = Runs.Directory(baselines, seed);

            if (!Directory.Exists(stored))
            {
                check.Fail(Invariant($"no baseline for seed {seed}. The gate compares the seeds it is given and does not quietly skip one."));

                continue;
            }

            if (!Current(check, stored, parameters, seed))
            {
                continue;
            }

            var difference = Comparison.FirstDifference(stored, Runs.Directory(arm, seed), Comparison.OutputFiles);

            if (difference is not null)
            {
                check.Fail(Invariant($"seed {seed}: {difference}"));
                check.Fail(
                    "The model has moved since the baseline was taken. If that was intended, say so and "
                    + "regenerate deliberately: dotnet run --project tools/Gates -- rebaseline. Regenerating is "
                    + "never a side effect of a comparison failing, because a baseline that repairs itself "
                    + "records nothing.");

                return;
            }
        }
    }

    /// <summary>
    /// Whether a stored baseline's configuration is still one this schema understands **and**
    /// still the configuration being run.
    ///
    /// A baseline compared against a run it does not describe is worse than no baseline: it passes
    /// or fails for reasons that have nothing to do with the model. So the stored file is loaded
    /// through the current loader and written back out; if the text does not survive the round trip,
    /// a parameter has been added, removed or renamed since, and the comparison stops here rather
    /// than proceeding on two configurations that are no longer the same shape.
    /// </summary>
    private static bool Current(GateCheck check, string stored, SimulationParameters parameters, int seed)
    {
        var path = Path.Combine(stored, "effective-config.toml");

        if (!File.Exists(path))
        {
            check.Fail(Invariant($"seed {seed}: the baseline has no effective-config.toml, so there is no saying what it is a baseline of"));

            return false;
        }

        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        var loaded = ConfigurationLoader.FromToml(text);

        if (loaded.IsFailed)
        {
            check.Fail(Invariant($"seed {seed}: the baseline's configuration no longer loads — {loaded.Errors[0].Message}. Regenerate it deliberately."));

            return false;
        }

        if (!string.Equals(loaded.Value.ToToml().ReplaceLineEndings("\n"), text, StringComparison.Ordinal))
        {
            check.Fail(Invariant(
                $"seed {seed}: the baseline's configuration no longer round-trips through the schema — a parameter has been added, removed or renamed since it was taken. Regenerate it deliberately rather than comparing anyway."));

            return false;
        }

        if (loaded.Value != parameters)
        {
            check.Fail(Invariant(
                $"seed {seed}: the baseline was taken under different parameters from the ones being run. Comparing them would report the configuration, not the model."));

            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes the baselines. **A separate command, and never a side effect of a comparison
    /// failing**: a baseline that repairs itself when it disagrees records nothing at all, and the
    /// first change it would have caught is the one that deletes it.
    /// </summary>
    public static GateReport Rebaseline(SimulationParameters parameters, IReadOnlyList<int> seeds, string? baselines = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);

        baselines ??= BaselineDirectory;

        var report = new GateReport("rebaseline — writing new committed baselines");
        var check = report.Check("the baselines are written");
        var (off, _) = Arms(parameters);

        try
        {
            if (Directory.Exists(baselines))
            {
                Directory.Delete(baselines, recursive: true);
            }

            Directory.CreateDirectory(baselines);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            check.Fail(Invariant($"could not clear {baselines}: {failure.Message}"));

            return report;
        }

        var ran = Runs.Execute(off, seeds, baselines);

        if (ran.IsFailed)
        {
            foreach (var error in ran.Errors)
            {
                check.Fail(error.Message);
            }

            return report;
        }

        File.WriteAllText(Path.Combine(baselines, ProvenanceFile), NewProvenance(off, seeds));

        check.Observe(Invariant($"{seeds.Count} seeds x {off.Run.Ticks} ticks written to {baselines}"));
        check.Observe("commit them, and say in the message why the model was expected to move");

        return report;
    }

    private static string NewProvenance(SimulationParameters parameters, IReadOnlyList<int> seeds)
    {
        var provenance = new System.Text.StringBuilder();

        provenance.AppendLine("# What these baselines were taken from. The effective configuration of");
        provenance.AppendLine("# each run is stored beside it; this is the rest of the answer.");
        provenance.AppendLine(Invariant($"commit = \"{Commit()}\""));
        provenance.AppendLine(Invariant($"taken = \"{DateTime.UtcNow:yyyy-MM-dd}\""));
        provenance.AppendLine(Invariant($"scenario = \"{parameters.Run.Scenario}\""));
        provenance.AppendLine(Invariant($"ticks = {parameters.Run.Ticks}"));
        provenance.AppendLine(Invariant($"seeds = [{string.Join(", ", seeds)}]"));

        return provenance.ToString();
    }

    private static string Provenance(string baselines)
    {
        var path = Path.Combine(baselines, ProvenanceFile);

        if (!File.Exists(path))
        {
            return "no provenance file; the baselines do not say what they came from";
        }

        var lines = File.ReadAllLines(path);

        return "baselines: " + string.Join("; ", lines.Where(l => l.Length > 0 && l[0] != '#'));
    }

    /// <summary>The commit the baselines were taken at, or a marker saying it could not be found. Never a guess.</summary>
    private static string Commit()
    {
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = WorkingTree.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (git is null)
            {
                return "unknown";
            }

            var head = git.StandardOutput.ReadToEnd().Trim();
            git.WaitForExit();

            return git.ExitCode == 0 && head.Length > 0 ? head : "unknown";
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "unknown";
        }
    }

    private static bool Ran(GateReport report, SimulationParameters parameters, IReadOnlyList<int> seeds, string into)
    {
        var run = Runs.Execute(parameters, seeds, into);

        if (run.IsFailed)
        {
            var check = report.Check(Invariant($"the {Path.GetFileName(into)} arm completes"));

            foreach (var error in run.Errors)
            {
                check.Fail(error.Message);
            }

            return false;
        }

        return true;
    }
}
