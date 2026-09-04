using EconomySimulation.Engine.Configuration;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// **V5a** — the identity archetype table reproduces the pre-archetype model byte for byte.
///
/// V5 says the credit machinery does not perturb the model when it is switched off. This says the
/// same of the archetype machinery, and it has to say it differently, because archetypes have no
/// switch: the mechanism is off when the table is the identity — one type, share 1.0, every `w` and
/// every `kappa` at 1.0 — which is what a configuration naming no archetype receives. A boolean
/// would have made this gate a test of the boolean.
///
/// **The comparison is against a fixture taken before E10 existed, and the fixture is never
/// regenerated.** When this gate goes red the cheap move is to take a new photograph, it is
/// available at any moment, and it destroys the only evidence that the mechanism is neutral when
/// off. A red V5a means one of two things: the identity table is not the identity, or the archetype
/// code perturbs a run it should not touch. Both are findings. Neither is fixed by photographing
/// the wrong thing. There is deliberately no `rebaseline` command here; the provenance file says
/// how to remake the fixture by hand, and why almost nothing justifies it.
///
/// What this gate cannot do, so that nobody expects it to: it says the identity table is neutral,
/// not that the `typed` table is correct. Nothing in this repository can say the second — the table
/// is a hypothesis about a population, defended by sweeping it (`02-PARAMETERS.md` §3.5) rather
/// than by a check.
/// </summary>
public static class ArchetypeGate
{
    /// <summary>Where the pre-archetype fixture lives, one directory per arm.</summary>
    public static string BaselineDirectory => Path.Combine(WorkingTree.Baselines, "archetypes");

    /// <summary>What the fixture was taken from, beside it. Read by a person, not by the gate.</summary>
    public const string ProvenanceFile = "provenance.toml";

    /// <summary>
    /// Both arms. `credit_off` alone would say only that the archetype code does not disturb a run
    /// in which most of the model is idle; `credit_high` is where the walk finances, reprices and
    /// rations, and it is the arm the result is read off.
    /// </summary>
    public static IReadOnlyList<string> Arms { get; } = ["credit_off", "credit_high"];

    // ---- the projection ---------------------------------------------------------------------

    /// <summary>
    /// Compared **whole**, byte for byte. E10 adds no column to either of these, and if it ever
    /// does, that is the failure this gate is for.
    /// </summary>
    public static IReadOnlyList<string> Whole { get; } = ["run.csv", "tiers.csv", "run.done"];

    /// <summary>
    /// Not compared, and named here rather than left to be inferred: a pre-E10 configuration has no
    /// `[archetypes]` section, and that absence is part of what is being tested. In its place the
    /// gate loads the stored file through the *current* schema and requires it to resolve to exactly
    /// the parameters being run — see <see cref="Describes"/>.
    /// </summary>
    public static IReadOnlyList<string> NotCompared { get; } = ["effective-config.toml"];

    /// <summary>
    /// The rest of the projection, stated so that a later story does not have to guess at it.
    ///
    /// Any **other** output file present in both the fixture and the run is compared on the columns
    /// their two headers share, and the gate reports how many it dropped. 10-04's cohort file gains
    /// an archetype column and 11-01's gains a per-good one; without this rule the gate goes
    /// permanently red the moment either lands, because a new column changes every line of that file
    /// under the identity table too.
    ///
    /// The alternative — teaching the writer to emit the v1 shape when there is one archetype — is
    /// worse, and it is worth saying why: it hides exactly the bug this gate exists to catch. A
    /// writer with a special case for the identity table would produce v1's file whether or not the
    /// model behind it had moved.
    ///
    /// A file present in only one of the two is named in the report and not compared. The fixture
    /// cannot speak to a file that did not exist when it was taken.
    /// </summary>
    public const string SharedColumnsRule =
        "any other output file: the columns the fixture and the run have in common";

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

        var report = new GateReport("V5a — the identity table reproduces the pre-archetype model");

        Identity(report.Check("the table under test is the identity table"), parameters);

        var against = report.Check("the run still matches the pre-archetype fixture");

        if (!Directory.Exists(baselines))
        {
            against.Fail(Invariant(
                $"no fixture at {baselines}. It is not generated by this gate — see {ProvenanceFile} in that directory for how it was taken and why it is not regenerated."));

            return report;
        }

        against.Observe(Provenance(baselines));
        against.Observe("compared whole: " + string.Join(", ", Whole));
        against.Observe("not compared: " + string.Join(", ", NotCompared));
        against.Observe(SharedColumnsRule);

        foreach (var arm in Arms)
        {
            Compare(report, against, parameters, seeds, workspace, baselines, arm);
        }

        return report;
    }

    /// <summary>
    /// The gate is worthless if the default has drifted into something else, so it says out loud
    /// what it is running.
    /// </summary>
    private static void Identity(GateCheck check, SimulationParameters parameters)
    {
        if (!parameters.Archetypes.IsIdentity)
        {
            check.Fail(Invariant(
                $"the parameters carry {parameters.Archetypes.Types.Count} archetype(s) and are not the identity table; this gate would then be comparing a typed run against an untyped fixture"));

            return;
        }

        check.Observe(Invariant(
            $"one type '{parameters.Archetypes.Types[0].Name}', share 1.0, every w and every kappa at 1.0, sigma_idio = {parameters.Archetypes.SigmaIdio}"));
    }

    private static void Compare(
        GateReport report,
        GateCheck check,
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        Workspace workspace,
        string baselines,
        string arm)
    {
        var stored = Path.Combine(baselines, arm);

        if (!Directory.Exists(stored))
        {
            check.Fail(Invariant($"no fixture for the {arm} arm at {stored}"));

            return;
        }

        var configured = Runs.Short(Scenarios.Apply(arm, parameters));
        var into = workspace.Arm(arm);
        var ran = Runs.Execute(configured, seeds, into);

        if (ran.IsFailed)
        {
            var failed = report.Check(Invariant($"the {arm} arm runs at all"));

            foreach (var error in ran.Errors)
            {
                failed.Fail(error.Message);
            }

            return;
        }

        foreach (var seed in seeds)
        {
            var storedSeed = Runs.Directory(stored, seed);

            if (!Directory.Exists(storedSeed))
            {
                check.Fail(Invariant(
                    $"{arm}: no fixture for seed {seed}. The gate compares the seeds it is given and does not quietly skip one."));

                continue;
            }

            var runSeed = Runs.Directory(into, seed);

            if (!Describes(check, storedSeed, configured, arm, seed))
            {
                continue;
            }

            var difference = Difference(check, storedSeed, runSeed, arm);

            if (difference is not null)
            {
                check.Fail(Invariant($"{arm}, seed {seed}: {difference}"));
                check.Fail(
                    "The identity table is not neutral. Either it is not the identity, or the archetype "
                    + "code is perturbing a run it should not touch. Both are findings; neither is fixed "
                    + "by regenerating the fixture, and there is no command here that would.");

                return;
            }
        }

        check.Observe(Invariant($"{arm}: {seeds.Count} seeds x {configured.Run.Ticks} ticks, identical"));
    }

    /// <summary>
    /// The fixture's stored configuration, loaded through the **current** schema, has to resolve to
    /// exactly the parameters being run.
    ///
    /// This is what replaces comparing `effective-config.toml` as text. The stored file has no
    /// `[archetypes]` section, and the whole claim of this gate is that an absent section resolves
    /// to the identity table and therefore to the same run. So: load it, and require the difference
    /// from the parameters under test to be empty. If a later change adds a section whose default is
    /// not what the fixture ran under, this names the key.
    /// </summary>
    private static bool Describes(
        GateCheck check,
        string stored,
        SimulationParameters parameters,
        string arm,
        int seed)
    {
        var path = Path.Combine(stored, "effective-config.toml");

        if (!File.Exists(path))
        {
            check.Fail(Invariant(
                $"{arm}, seed {seed}: the fixture has no effective-config.toml, so there is no saying what it is a fixture of"));

            return false;
        }

        var loaded = ConfigurationLoader.FromToml(File.ReadAllText(path));

        if (loaded.IsFailed)
        {
            var why = string.Join("; ", loaded.Errors.Select(e => e.Message));

            check.Fail(Invariant($"{arm}, seed {seed}: the fixture's configuration no longer loads — {why}"));

            return false;
        }

        var differences = loaded.Value.DifferencesFrom(parameters);

        if (differences.Count > 0)
        {
            var moved = string.Join(", ", differences);

            check.Fail(Invariant(
                $"{arm}, seed {seed}: the fixture was taken under different parameters from the ones being run ({moved}). Comparing them would report the configuration, not the model."));

            return false;
        }

        return true;
    }

    private static Difference? Difference(GateCheck check, string stored, string run, string arm)
    {
        var whole = Comparison.FirstDifference(stored, run, Whole);

        if (whole is not null)
        {
            return whole;
        }

        foreach (var file in Others(stored, run))
        {
            var storedHas = File.Exists(Path.Combine(stored, file));
            var runHas = File.Exists(Path.Combine(run, file));

            if (!storedHas || !runHas)
            {
                var side = storedHas ? "fixture" : "run";

                check.Observe(Invariant(
                    $"{arm}: {file} is present only in the {side} and is not compared — the fixture cannot speak to a file that did not exist when it was taken"));

                continue;
            }

            var shared = Comparison.FirstDifferenceOnSharedColumns(stored, run, file, out var dropped);

            if (dropped > 0)
            {
                check.Observe(Invariant($"{arm}: {file} compared on the shared columns; {dropped} not compared"));
            }

            if (shared is not null)
            {
                return shared;
            }
        }

        return null;
    }

    /// <summary>Every file in either directory that the projection does not name, in name order.</summary>
    private static IReadOnlyList<string> Others(string stored, string run)
    {
        var named = Whole.Concat(NotCompared).ToHashSet(StringComparer.Ordinal);

        return
        [
            .. Directory.EnumerateFiles(stored)
                .Concat(Directory.EnumerateFiles(run))
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(f => !named.Contains(f))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(f => f, StringComparer.Ordinal),
        ];
    }

    private static string Provenance(string baselines)
    {
        var path = Path.Combine(baselines, ProvenanceFile);

        if (!File.Exists(path))
        {
            return "no provenance file; the fixture does not say what it came from";
        }

        var lines = File.ReadAllLines(path).Where(l => l.Length > 0 && l[0] != '#');

        return "fixture: " + string.Join("; ", lines);
    }
}
