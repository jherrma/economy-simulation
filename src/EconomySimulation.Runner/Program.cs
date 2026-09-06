using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Runner;

/// <summary>
/// One run — one scenario, one seed, one directory — and nothing else. No model logic lives here,
/// and no analysis: the engine writes CSV and this parses arguments.
///
/// One run per process is what the campaign is built out of (09-02), and it is also the thing a
/// person needs when a single seed of a finished campaign has to be reproduced or looked at under a
/// debugger. Those are the same command, deliberately: a reproduction that goes through a different
/// path than the original is not a reproduction.
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var arguments = Arguments.Parse(args);

        if (arguments.IsFailed)
        {
            return Usage(arguments.Errors);
        }

        var (scenarioName, seed, into, scenarios, calibration) = arguments.Value;
        var scenario = Scenario.FromFile(Path.Combine(scenarios, scenarioName + ".toml"));

        if (scenario.IsFailed)
        {
            return Fail(scenario.Errors);
        }

        var basis = Basis(calibration);

        if (basis.IsFailed)
        {
            return Fail(basis.Errors);
        }

        // The calibration is the **basis** and the scenario goes on top of it. The other order
        // would work only by accident — it relies on the two never setting the same key — and
        // `run.replacement` is a key they both do set.
        var applied = scenario.Value.ApplyTo(basis.Value);

        if (applied.IsFailed)
        {
            return Fail(applied.Errors);
        }

        var ran = Execute(applied.Value, seed, into);

        if (ran.IsFailed)
        {
            return Fail(ran.Errors);
        }

        Console.Out.WriteLine(Invariant($"{scenarioName} seed {seed} -> {into}"));

        return 0;
    }

    /// <summary>
    /// The marker is written last and only on a clean finish. A halted run leaves a plausible
    /// partial CSV behind, and every consumer of this output is required to look for the marker
    /// rather than for the file.
    /// </summary>
    private static Result Execute(SimulationParameters parameters, int seed, string into)
    {
        var simulation = new Simulation(parameters, seed);
        var opened = simulation.Start();

        if (opened.IsFailed)
        {
            return opened;
        }

        var created = MetricsWriter.Create(simulation, into);

        if (created.IsFailed)
        {
            return created.ToResult();
        }

        using var writer = created.Value;

        for (var tick = 1; tick <= parameters.Run.Ticks; tick++)
        {
            var ticked = simulation.RunTick(tick);

            if (ticked.IsFailed)
            {
                return ticked;
            }

            var written = writer.Write(simulation);

            if (written.IsFailed)
            {
                return written;
            }
        }

        return writer.Finish();
    }

    /// <summary>
    /// The goods table the scenario is laid on: the schema defaults, or a calibration file.
    ///
    /// A calibration is not a scenario and is not one of the campaign's arms. It is the town the
    /// experiment is run in — §3.1's six goods or §3.6's eighteen — and every arm has to be run in
    /// the same one or the comparison is between two towns.
    /// </summary>
    private static Result<SimulationParameters> Basis(string? calibration) =>
        calibration is null
            ? Result.Ok(SimulationParameters.Default)
            : ConfigurationLoader.FromFile(calibration);

    private sealed record Arguments(string Scenario, int Seed, string Into, string Scenarios, string? Calibration)
    {
        internal static Result<Arguments> Parse(string[] args)
        {
            string? scenario = null;
            string? into = null;
            var scenarios = Path.Combine("config", "scenarios");
            string? calibration = null;
            int? seed = null;

            for (var i = 0; i < args.Length; i++)
            {
                if (i + 1 >= args.Length)
                {
                    return Result.Fail<Arguments>(Invariant($"{args[i]}: expected a value after it, got nothing"));
                }

                var value = args[i + 1];
                i++;

                switch (args[i - 1])
                {
                    case "--scenario":
                        scenario = value;
                        break;
                    case "--seed" when int.TryParse(value, out var parsed):
                        seed = parsed;
                        break;
                    case "--seed":
                        return Result.Fail<Arguments>(Invariant($"--seed: expected a whole number, got {value}"));
                    case "--into":
                        into = value;
                        break;
                    case "--scenarios":
                        scenarios = value;
                        break;
                    case "--calibration":
                        calibration = value;
                        break;
                    default:
                        return Result.Fail<Arguments>(Invariant($"{args[i - 1]}: not an argument this takes"));
                }
            }

            if (scenario is null || seed is null || into is null)
            {
                return Result.Fail<Arguments>("--scenario, --seed and --into are all required");
            }

            return Result.Ok(new Arguments(scenario, seed.Value, into, scenarios, calibration));
        }
    }

    private static int Fail(IEnumerable<IError> errors)
    {
        foreach (var error in errors)
        {
            Console.Error.WriteLine("economy-simulation: " + error.Message);
        }

        return 1;
    }

    private static int Usage(IEnumerable<IError> errors)
    {
        Fail(errors);

        Console.Error.WriteLine();
        Console.Error.WriteLine("usage: economy-simulation --scenario <name> --seed <n> --into <directory>");
        Console.Error.WriteLine("                          [--scenarios <directory>] [--calibration <file>]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  Writes run.csv, tiers.csv, effective-config.toml and, on a clean");
        Console.Error.WriteLine("  finish, the run.done marker. Scenarios default to config/scenarios.");
        Console.Error.WriteLine("  --calibration lays the scenario on a goods table other than the");
        Console.Error.WriteLine("  schema defaults, e.g. config/calibrations/grouped.toml.");

        return 2;
    }
}
