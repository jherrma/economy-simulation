using EconomySimulation.Engine.Configuration;

namespace EconomySimulation.Gates;

/// <summary>
/// The goods table a gate runs on: §3.1's six rows by default, §3.6's eighteen when asked.
///
/// A calibration is a **basis**, not a scenario. It is loaded first and the scenario files are laid
/// on top of it (<see cref="Scenarios.Apply"/>), which is the only composition order that is not an
/// accident: composing the other way round relies on the calibration and the scenario never touching
/// the same key, and nobody would notice that going wrong.
///
/// Read from this checkout's working tree rather than from the current directory, for the same
/// reason the baselines are: a gate should report on *this* commit however it was launched.
/// </summary>
public static class Calibration
{
    /// <summary>The name a gate is asked for on the command line.</summary>
    public const string GroupedName = "grouped";

    private static readonly Lazy<SimulationParameters> Eighteen = new(() => Load("grouped.toml"));

    /// <summary>Where the calibrations live.</summary>
    public static string Directory => Path.Combine(WorkingTree.Root, "config", "calibrations");

    /// <summary>
    /// §3.6's eighteen goods, at the 5,000 households its own thin-shelf floor forces, under the
    /// hazard the table declares it needs.
    /// </summary>
    public static SimulationParameters Grouped => Eighteen.Value;

    /// <summary>
    /// The basis named on the command line: `grouped` for §3.6's table, the schema defaults
    /// otherwise.
    /// </summary>
    public static SimulationParameters From(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return Array.Exists(args, a => string.Equals(a, GroupedName, StringComparison.Ordinal))
            ? Grouped
            : SimulationParameters.Default;
    }

    /// <summary>Whether the run is on something other than §3.1 — for a report that says so.</summary>
    public static string NameOf(SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters.Categories.Count == SimulationParameters.Default.Categories.Count
            ? "§3.1, six goods"
            : $"§3.6, {parameters.Categories.Count} goods";
    }

    private static SimulationParameters Load(string file)
    {
        var path = Path.Combine(Directory, file);
        var loaded = ConfigurationLoader.FromFile(path);

        if (loaded.IsFailed)
        {
            throw new InvalidOperationException(
                $"{path} does not load: {string.Join("; ", loaded.Errors.Select(e => e.Message))}");
        }

        return loaded.Value;
    }
}
