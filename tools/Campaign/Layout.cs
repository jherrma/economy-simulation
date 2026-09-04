using static System.FormattableString;

namespace EconomySimulation.Campaign;

/// <summary>
/// Where everything is: the working tree, the runner to spawn, and one directory per run.
///
/// Found by walking up from the assembly rather than taken from the current directory, so a
/// campaign uses the scenarios and the runner of *this* checkout however it was launched.
/// </summary>
public static class Layout
{
    public static string Root { get; } = FindRoot();

    public static string Scenarios => Path.Combine(Root, "config", "scenarios");

    /// <summary>Where a campaign writes, unless told otherwise. Not committed.</summary>
    public static string DefaultOutput => Path.Combine(Root, "campaign");

    /// <summary>One directory per (scenario, seed), named so the pairing is readable off the tree.</summary>
    public static string Run(string output, string scenario, int seed) =>
        Path.Combine(output, scenario, Invariant($"seed-{seed}"));

    /// <summary>The collected dataset, beside the runs it was built from.</summary>
    public static string Dataset(string output) => Path.Combine(output, "dataset");

    public static string Manifest(string output) => Path.Combine(output, "manifest.toml");

    /// <summary>
    /// The runner executable of this checkout, built in the same configuration as this campaign.
    ///
    /// Taken from this assembly's own path rather than hard-coded, so a Release campaign never
    /// silently spawns a Debug engine — which would produce a dataset nobody could reproduce from
    /// the commit it claims to come from.
    /// </summary>
    public static string Runner()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        var framework = here.Name;
        var configuration = here.Parent?.Name
            ?? throw new InvalidOperationException($"Cannot read a build configuration out of {AppContext.BaseDirectory}.");

        var directory = Path.Combine(
            Root, "src", "EconomySimulation.Runner", "bin", configuration, framework);

        var apphost = Path.Combine(directory, "economy-simulation");

        if (File.Exists(apphost))
        {
            return apphost;
        }

        var library = Path.Combine(directory, "economy-simulation.dll");

        if (File.Exists(library))
        {
            return library;
        }

        throw new InvalidOperationException(
            $"No runner in {directory}. Build the solution in {configuration} first: the campaign "
            + "spawns the runner rather than calling into it, so a missing build is a missing "
            + "engine rather than a compile error.");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EconomySimulation.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No EconomySimulation.slnx above {AppContext.BaseDirectory}; the campaign cannot find "
            + "the scenarios or the runner.");
    }
}
