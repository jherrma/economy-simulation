using EconomySimulation.Engine.Configuration;

namespace EconomySimulation.Gates;

/// <summary>
/// The campaign's own scenario files (09-01), applied to whatever parameters a gate is working
/// with.
///
/// The gates used to carry their own definitions of `credit_off` and `credit_high`. Two definitions
/// that drift apart would make a green gate say nothing about the runs that produce the results, so
/// there is now one: `config/scenarios`, read from this checkout's working tree.
/// </summary>
public static class Scenarios
{
    private static readonly Lazy<IReadOnlyList<Scenario>> Committed = new(Load);

    /// <summary>Where the five live, in this checkout rather than in the current directory.</summary>
    public static string Directory => Path.Combine(WorkingTree.Root, "config", "scenarios");

    /// <summary>The baseline. It is the defaults, because every switch defaults to off.</summary>
    public static SimulationParameters CreditOff(SimulationParameters parameters) =>
        Apply("credit_off", parameters);

    /// <summary>`θ ~ U(0.4, 0.9)`, credit on. The treatment arm of the experiment.</summary>
    public static SimulationParameters CreditHigh(SimulationParameters parameters) =>
        Apply("credit_high", parameters);

    /// <summary>
    /// One named scenario laid on top of the given parameters, keeping whatever the caller has
    /// already changed for its own reasons.
    /// </summary>
    public static SimulationParameters Apply(string name, SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var scenario = Committed.Value.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No scenario called '{name}' in {Directory}.");

        var applied = scenario.ApplyTo(parameters);

        // A gate whose scenario will not load onto its own parameters has nothing to measure, and
        // saying so here beats reporting a failure against the wrong configuration.
        return applied.IsSuccess
            ? applied.Value
            : throw new InvalidOperationException(
                $"Scenario '{name}' does not apply to these parameters: "
                + string.Join("; ", applied.Errors.Select(e => e.Message)));
    }

    private static IReadOnlyList<Scenario> Load()
    {
        var all = Scenario.All(Directory);

        return all.IsSuccess
            ? all.Value
            : throw new InvalidOperationException(
                $"The scenarios in {Directory} do not load: "
                + string.Join("; ", all.Errors.Select(e => e.Message)));
    }
}
