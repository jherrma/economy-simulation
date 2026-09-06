using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Campaign;

/// <summary>
/// The same households abstain, and are the same type, in every scenario, seed by seed.
///
/// This is the entire statistical strategy: the same twenty per cent of the town, the same seeds,
/// one mechanism changed. It fails silently — a campaign whose abstainer draw moved between arms
/// produces a perfectly well-formed dataset in which the headline difference is partly a different
/// set of people — so it is checked on every campaign rather than once in a test.
///
/// The archetype assignment (E10) is checked the same way and for the same reason. Note that it is
/// the **assignment** that is compared, household by household, not the count per type: two arms
/// with three hundred prudent households each can still be two different three hundred.
/// </summary>
public static class Pairing
{
    /// <summary>
    /// Builds each run's opening world and compares who abstains against the baseline scenario.
    ///
    /// Opening the world is cheap next to running it, and it is the only place the abstainer set
    /// exists as a set: the output carries a count, and equal counts are not the same claim.
    /// </summary>
    public static Result Check(
        IReadOnlyList<Scenario> scenarios,
        IReadOnlyList<int> seeds,
        SimulationParameters? calibration = null)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(seeds);

        if (scenarios.Count == 0)
        {
            return Result.Fail("pairing: no scenarios to compare");
        }

        var problems = new List<IError>();
        var baseline = scenarios[0];

        // Every arm is laid on the same calibration, because the calibration is the *town* rather
        // than a treatment. Resolved once here rather than read off `scenario.Parameters`, which is
        // always the schema defaults and would silently pair the wrong worlds on a grouped campaign.
        var basis = calibration ?? SimulationParameters.Default;
        var applied = new Dictionary<string, SimulationParameters>(StringComparer.Ordinal);

        foreach (var scenario in scenarios)
        {
            var result = scenario.ApplyTo(basis);

            if (result.IsFailed)
            {
                return Result.Fail(result.Errors);
            }

            applied[scenario.Name] = result.Value;
        }

        // Each scenario's archetype control is the **first scenario of its own row of §3.5's sweep
        // grid**, and the untyped baseline for anything the grid does not name. A typed scenario is
        // a different population from an untyped one by construction — that is what a table is — so
        // comparing every arm against `credit_off` would fail by design and say nothing.
        //
        // The abstainer set has no such qualification: it is drawn from its own stream and does not
        // depend on the table, so it is compared against the one baseline throughout, and a typed
        // scenario whose abstainers moved is still void.
        var controls = Controls(scenarios);

        foreach (var seed in seeds)
        {
            var expected = Opening(applied[baseline.Name], seed);
            var opened = new Dictionary<string, (HashSet<int> Abstainers, int[] Archetypes)>(StringComparer.Ordinal);

            foreach (var scenario in scenarios.Skip(1))
            {
                var actual = Opening(applied[scenario.Name], seed);
                var control = controls[scenario.Name];

                if (!ReferenceEquals(control, scenario))
                {
                    if (!opened.TryGetValue(control.Name, out var expectedTypes))
                    {
                        expectedTypes = Opening(applied[control.Name], seed);
                        opened[control.Name] = expectedTypes;
                    }

                    // The table first: two arms of one grid row that do not carry the same table
                    // are not a comparison at all, and eight hand-written scenario files is exactly
                    // where that typo lives.
                    Same(problems, seed, scenario, control, applied);
                    Compare(problems, seed, scenario.Name, control.Name, expectedTypes.Archetypes, actual.Archetypes);
                }

                if (!expected.Abstainers.SetEquals(actual.Abstainers))
                {
                    var moved = expected.Abstainers.Count == actual.Abstainers.Count
                        ? Invariant($"{expected.Abstainers.Except(actual.Abstainers).Count()} of {expected.Abstainers.Count} households swapped")
                        : Invariant($"{expected.Abstainers.Count} against {actual.Abstainers.Count} households");

                    problems.Add(new Error(Invariant(
                        $"seed {seed}: the abstainers of {scenario.Name} are not those of {baseline.Name} ({moved}) — the comparison is not paired and the campaign is void")));
                }
            }
        }

        return problems.Count > 0 ? Result.Fail(problems) : Result.Ok();
    }

    /// <summary>
    /// Which scenario each one's archetype table is checked against: the first scenario of its own
    /// row of the sweep grid, and the campaign's baseline for anything the grid does not name.
    /// </summary>
    private static Dictionary<string, Scenario> Controls(IReadOnlyList<Scenario> scenarios)
    {
        var controls = new Dictionary<string, Scenario>(StringComparer.Ordinal);

        foreach (var scenario in scenarios)
        {
            controls[scenario.Name] = scenarios[0];
        }

        foreach (var (_, row) in Scenario.SweepGrid)
        {
            var control = scenarios.FirstOrDefault(s => row.Contains(s.Name, StringComparer.Ordinal));

            if (control is null)
            {
                continue;
            }

            foreach (var name in row.Where(controls.ContainsKey))
            {
                controls[name] = control;
            }
        }

        return controls;
    }

    /// <summary>
    /// Two scenarios meant to be compared have to carry the **same** archetype table, or the
    /// difference between them is the table and the mechanism together.
    /// </summary>
    private static void Same(
        List<IError> problems,
        int seed,
        Scenario scenario,
        Scenario control,
        IReadOnlyDictionary<string, SimulationParameters> applied)
    {
        if (seed != 1 || applied[scenario.Name].Archetypes == applied[control.Name].Archetypes)
        {
            return;
        }

        var keys = applied[scenario.Name]
            .DifferencesFrom(applied[control.Name])
            .Where(k => k.StartsWith("archetypes", StringComparison.Ordinal));

        problems.Add(new Error(Invariant(
            $"{scenario.Name} carries a different archetype table from {control.Name} ({string.Join(", ", keys)}) — comparing them would measure the table and the mechanism together and attribute both to the mechanism")));
    }

    /// <summary>
    /// The assignment, not the count per type. Equal counts are not the same claim: two arms with
    /// three hundred prudent households each can still be two different three hundred.
    /// </summary>
    private static void Compare(
        List<IError> problems,
        int seed,
        string scenario,
        string control,
        int[] expected,
        int[] actual)
    {
        if (expected.SequenceEqual(actual))
        {
            return;
        }

        var swapped = expected.Length == actual.Length
            ? Invariant($"{expected.Where((a, h) => a != actual[h]).Count()} of {expected.Length} households changed type")
            : Invariant($"{expected.Length} against {actual.Length} households");

        problems.Add(new Error(Invariant(
            $"seed {seed}: the archetypes of {scenario} are not those of {control} ({swapped}) — the comparison is not paired and the campaign is void")));
    }

    private static (HashSet<int> Abstainers, int[] Archetypes) Opening(SimulationParameters parameters, int seed)
    {
        var simulation = new Simulation(parameters, seed);
        var opened = simulation.Start();

        if (opened.IsFailed)
        {
            throw new InvalidOperationException(
                "pairing: " + string.Join("; ", opened.Errors.Select(e => e.Message)));
        }

        var abstainers = new HashSet<int>();

        for (var household = 0; household < simulation.Population.IsAbstainer.Length; household++)
        {
            if (simulation.Population.IsAbstainer[household])
            {
                abstainers.Add(household);
            }
        }

        return (abstainers, [.. simulation.Population.Archetype]);
    }
}
