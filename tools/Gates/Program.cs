using EconomySimulation.Engine.Configuration;
using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// Runs one gate and reports what it found.
///
/// Exit zero means every check in the gate held. Exit one means one did not, and the report says
/// which file, row and column stopped agreeing — a gate that exits non-zero without saying where is
/// a gate people learn to re-run rather than read.
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Usage("no gate named");
        }

        var name = args[0];
        var keep = Array.Exists(args, a => string.Equals(a, "--keep", StringComparison.Ordinal));

        using var workspace = Workspace.Create(name, keep);

        var report = name switch
        {
            "determinism" => DeterminismGate.Run(SimulationParameters.Default, Runs.ShortSeeds, workspace),
            _ => null,
        };

        if (report is null)
        {
            return Usage(Invariant($"no gate called '{name}'"));
        }

        Console.Out.Write(report.ToString());

        if (keep)
        {
            Console.Out.WriteLine(Invariant($"runs kept in {workspace.Root}"));
        }

        return report.Passed ? 0 : 1;
    }

    private static int Usage(string problem)
    {
        Console.Error.WriteLine(Invariant($"gates: {problem}."));
        Console.Error.WriteLine("usage: dotnet run --project tools/Gates -- <gate> [--keep]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  determinism   V2 — the same seed produces the same run, twice and across threads");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  --keep        leave the runs on disk instead of removing them");

        return 2;
    }
}
