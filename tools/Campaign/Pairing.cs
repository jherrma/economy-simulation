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
    public static Result Check(IReadOnlyList<Scenario> scenarios, IReadOnlyList<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(seeds);

        if (scenarios.Count == 0)
        {
            return Result.Fail("pairing: no scenarios to compare");
        }

        var problems = new List<IError>();
        var baseline = scenarios[0];

        foreach (var seed in seeds)
        {
            var expected = Opening(baseline.Parameters, seed);

            foreach (var scenario in scenarios.Skip(1))
            {
                var actual = Opening(scenario.Parameters, seed);

                if (!expected.Abstainers.SetEquals(actual.Abstainers))
                {
                    var moved = expected.Abstainers.Count == actual.Abstainers.Count
                        ? Invariant($"{expected.Abstainers.Except(actual.Abstainers).Count()} of {expected.Abstainers.Count} households swapped")
                        : Invariant($"{expected.Abstainers.Count} against {actual.Abstainers.Count} households");

                    problems.Add(new Error(Invariant(
                        $"seed {seed}: the abstainers of {scenario.Name} are not those of {baseline.Name} ({moved}) — the comparison is not paired and the campaign is void")));
                }

                // The assignment, not the count per type. Equal counts are not the same claim, and
                // a table whose types were reordered between two arms would produce exactly that.
                if (!expected.Archetypes.SequenceEqual(actual.Archetypes))
                {
                    var swapped = expected.Archetypes.Length == actual.Archetypes.Length
                        ? Invariant($"{expected.Archetypes.Where((a, h) => a != actual.Archetypes[h]).Count()} of {expected.Archetypes.Length} households changed type")
                        : Invariant($"{expected.Archetypes.Length} against {actual.Archetypes.Length} households");

                    problems.Add(new Error(Invariant(
                        $"seed {seed}: the archetypes of {scenario.Name} are not those of {baseline.Name} ({swapped}) — the comparison is not paired and the campaign is void")));
                }
            }
        }

        return problems.Count > 0 ? Result.Fail(problems) : Result.Ok();
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
