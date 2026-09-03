using EconomySimulation.Engine.Configuration;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// config/default.toml is generated from the schema rather than typed, and committed. Every test
/// that needs a configuration starts from it, so "the defaults" means one file rather than
/// whatever each test happened to construct.
///
/// This test writes it when it is missing and compares it when it is there, which makes the
/// committed file impossible to leave stale: change a default, and either the diff shows up in
/// the commit or this fails.
/// </summary>
public sealed class FixtureGenerator
{
    internal static string DefaultConfigPath => Path.Combine(Repo.Root, "config", "default.toml");

    [Fact]
    public void TheCommittedDefaultConfigIsWhatTheSchemaSays()
    {
        var expected = SimulationParameters.Default.ToToml();

        if (!File.Exists(DefaultConfigPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultConfigPath)!);
            File.WriteAllText(DefaultConfigPath, expected);
        }

        Assert.Equal(expected, File.ReadAllText(DefaultConfigPath).ReplaceLineEndings("\n"));
    }
}
