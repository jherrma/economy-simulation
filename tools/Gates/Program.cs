using EconomySimulation.Engine;
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
            "neutrality" => NeutralityGate.Run(SimulationParameters.Default, Runs.WindowSeeds, workspace),
            "nullrun" => NullRunGate.Run(SimulationParameters.Default, Runs.CampaignSeeds(SimulationParameters.Default), workspace),
            "creditoff" => CreditOffGate.Run(SimulationParameters.Default, Runs.ShortSeeds, workspace),
            "rebaseline" => CreditOffGate.Rebaseline(SimulationParameters.Default, Runs.ShortSeeds),
            "pilot" => PilotProbe.Run(Probe(args, SimulationParameters.Default), Runs.CampaignSeeds(SimulationParameters.Default), workspace),
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

    /// <summary>
    /// `pilot k=0.1 rate=13` reruns the probe with one parameter moved. The two sensitivities
    /// worth asking: the adjustment speed sets the price level (§10.4), and the loan rate is the
    /// one parameter a reader will check against their own credit card.
    /// </summary>
    private static SimulationParameters Probe(string[] args, SimulationParameters parameters)
    {
        foreach (var argument in args)
        {
            var split = argument.IndexOf('=', StringComparison.Ordinal);

            if (split < 1 || !double.TryParse(
                    argument[(split + 1)..],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                continue;
            }

            parameters = argument[..split] switch
            {
                "k" => parameters with { Prices = parameters.Prices with { K = value } },
                "rate" => parameters with { Credit = parameters.Credit with { LoanRate = new Rate(value) } },
                _ => parameters,
            };
        }

        return parameters;
    }

    private static int Usage(string problem)
    {
        Console.Error.WriteLine(Invariant($"gates: {problem}."));
        Console.Error.WriteLine("usage: dotnet run --project tools/Gates -- <gate> [--keep]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  determinism   V2 — the same seed produces the same run, twice and across threads");
        Console.Error.WriteLine("  neutrality    V3 — multiply every nominal quantity by c and nothing real moves");
        Console.Error.WriteLine("  nullrun       V4 — the creditless baseline sits still after the warm-up");
        Console.Error.WriteLine("  creditoff     V5 — credit_high with theta = 0 reproduces credit_off, byte for byte");
        Console.Error.WriteLine("  pilot         not a gate — how finely the campaign's seed set resolves the headline");
        Console.Error.WriteLine("  rebaseline    write new committed baselines for V5 — deliberate, never automatic");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  --keep        leave the runs on disk instead of removing them");

        return 2;
    }
}
