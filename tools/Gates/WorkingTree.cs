namespace EconomySimulation.Gates;

/// <summary>
/// Locates the working tree, for the baselines the credit-off gate keeps under version control.
///
/// Found by walking up from the assembly rather than taken from the current directory, so that a
/// gate compares against the baselines of *this* checkout however it was launched.
/// </summary>
public static class WorkingTree
{
    public static string Root { get; } = FindRoot();

    /// <summary>Where the committed baselines live: one directory per scenario, one per seed inside it.</summary>
    public static string Baselines => Path.Combine(Root, "tools", "Gates", "baselines");

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
            $"No EconomySimulation.slnx above {AppContext.BaseDirectory}; the gates cannot find "
            + "the committed baselines.");
    }
}
