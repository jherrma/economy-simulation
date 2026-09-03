namespace EconomySimulation.Tests.Infrastructure;

/// <summary>Locates the working tree, for the tests that read the engine's own source.</summary>
internal static class Repo
{
    internal static string Root { get; } = FindRoot();

    internal static string EngineSourceDirectory =>
        Path.Combine(Root, "src", "EconomySimulation.Engine");

    internal static IEnumerable<string> EngineSources() =>
        Directory
            .EnumerateFiles(EngineSourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EconomySimulation.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"No EconomySimulation.slnx above {AppContext.BaseDirectory}; the source-scanning "
            + "tests cannot find the working tree.");
    }
}
