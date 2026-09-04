using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using static System.FormattableString;

namespace EconomySimulation.Campaign;

/// <summary>
/// What this dataset is, written beside it.
///
/// A CSV whose engine commit and parameters cannot be recovered is not a result. This is what makes
/// one citable six months from now, and it records the configuration by **hash of the effective
/// configuration** rather than by scenario name, because the name is what somebody meant and the
/// hash is what actually ran.
/// </summary>
public static class Manifest
{
    public static string Write(
        string output,
        IReadOnlyList<string> scenarios,
        IReadOnlyList<int> seeds,
        Collector.Collected collected,
        DateTimeOffset started,
        DateTimeOffset finished)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(collected);

        var (commit, clean) = Commit();
        var manifest = new StringBuilder();

        manifest.AppendLine("# What this dataset is. Written by the campaign runner (09-02).");
        manifest.AppendLine("#");
        manifest.AppendLine("# The configuration is recorded by the hash of the effective configuration each");
        manifest.AppendLine("# scenario actually ran, not by its name: the name is what somebody meant.");
        manifest.AppendLine();

        manifest.AppendLine("[campaign]");
        manifest.AppendLine(CultureInfo.InvariantCulture, $"started = \"{started:O}\"");
        manifest.AppendLine(CultureInfo.InvariantCulture, $"finished = \"{finished:O}\"");
        manifest.AppendLine(CultureInfo.InvariantCulture, $"runs = {scenarios.Count * seeds.Count}");
        manifest.AppendLine(CultureInfo.InvariantCulture, $"engine_commit = \"{commit}\"");
        manifest.AppendLine(CultureInfo.InvariantCulture, $"working_tree_clean = {(clean ? "true" : "false")}");
        manifest.AppendLine(CultureInfo.InvariantCulture, $"seeds = [{string.Join(", ", seeds)}]");
        manifest.AppendLine();

        manifest.AppendLine("[dataset]");

        foreach (var (file, rows) in collected.Rows.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            manifest.AppendLine(CultureInfo.InvariantCulture, $"\"{file}\" = {rows}");
        }

        manifest.AppendLine();

        foreach (var scenario in scenarios)
        {
            manifest.AppendLine(CultureInfo.InvariantCulture, $"[scenarios.{scenario}]");
            manifest.AppendLine(CultureInfo.InvariantCulture, $"effective_config_sha256 = \"{Hash(collected.Configurations[scenario])}\"");
            manifest.AppendLine();
        }

        var path = Layout.Manifest(output);
        File.WriteAllText(path, manifest.ToString());

        return path;
    }

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>
    /// The commit, and whether anything was uncommitted when the campaign ran.
    ///
    /// A dirty tree is recorded rather than refused: a campaign run while trying something out is a
    /// legitimate thing to do, and one that says so cannot later be mistaken for a reproducible one.
    /// </summary>
    private static (string Commit, bool Clean) Commit()
    {
        var commit = Git("rev-parse HEAD");
        var status = Git("status --porcelain");

        return commit is null || status is null
            ? ("unknown — no git in this tree", false)
            : (commit.Trim(), status.Trim().Length == 0);
    }

    private static string? Git(string arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = Layout.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments.Split(' '))
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception problem) when (problem is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No git, or no repository. The manifest says so rather than the campaign failing:
            // provenance that is missing should be visibly missing.
            return null;
        }
    }
}
