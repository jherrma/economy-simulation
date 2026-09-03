using EconomySimulation.Engine;
using EconomySimulation.Engine.Configuration;
using EconomySimulation.Engine.Output;
using FluentResults;

namespace EconomySimulation.Gates;

/// <summary>
/// One run of the engine, written to a directory, exactly as the campaign will run it.
///
/// The gates go through this rather than through <see cref="Simulation.Run"/> because what they
/// compare is the output files. A gate comparing object graphs would pass on a model whose writer
/// was broken, and the writer is the only thing any result is ever read from.
/// </summary>
public static class Runs
{
    /// <summary>
    /// Short enough to run after every change, long enough to catch what a full run would.
    ///
    /// The temptation is to check determinism on a realistic run; the value is in being able to
    /// run it at all times, and forty-eight ticks on four seeds exercises every branch a 360-tick
    /// run does — the warm-up transient, repricing, rationing, origination and repayment all
    /// happen inside the first year.
    /// </summary>
    public const int ShortTicks = 48;

    /// <summary>The four seeds the short gates use. A subset of the campaign's, so a gate failure is reproducible in a full run.</summary>
    public static IReadOnlyList<int> ShortSeeds { get; } = [1, 2, 3, 4];

    /// <summary>The parameters of a short run: the given configuration, over <see cref="ShortTicks"/> ticks.</summary>
    public static SimulationParameters Short(SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters with { Run = parameters.Run with { Ticks = ShortTicks } };
    }

    /// <summary>
    /// Runs one seed into one directory and finishes cleanly, or fails with the engine's own
    /// refusal. `afterTick` sees the simulation between the tick and the row written from it — the
    /// one place a gate can observe something the CSV does not carry.
    /// </summary>
    public static Result Execute(
        SimulationParameters parameters,
        int seed,
        string directory,
        Action<Simulation>? afterTick = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var simulation = new Simulation(parameters, seed);
        var opening = simulation.Start();

        if (opening.IsFailed)
        {
            return opening;
        }

        var created = MetricsWriter.Create(simulation, directory);

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

            afterTick?.Invoke(simulation);

            var written = writer.Write(simulation);

            if (written.IsFailed)
            {
                return written;
            }
        }

        // The marker is written last and only here: a halted run leaves a plausible partial CSV,
        // and every consumer of this output requires the marker rather than the file.
        return writer.Finish();
    }

    /// <summary>
    /// Runs several seeds, one directory each, named `seed-<n>` under <paramref name="into"/>.
    /// Serial by default; <paramref name="threaded"/> is what V2's second assertion runs.
    /// </summary>
    public static Result Execute(
        SimulationParameters parameters,
        IReadOnlyList<int> seeds,
        string into,
        bool threaded = false)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(seeds);

        var failures = new List<IError>();

        if (threaded)
        {
            var lockObject = new Lock();

            Parallel.ForEach(seeds, seed =>
            {
                var run = Execute(parameters, seed, Directory(into, seed));

                if (run.IsFailed)
                {
                    lock (lockObject)
                    {
                        failures.AddRange(run.Errors);
                    }
                }
            });
        }
        else
        {
            foreach (var seed in seeds)
            {
                var run = Execute(parameters, seed, Directory(into, seed));

                if (run.IsFailed)
                {
                    failures.AddRange(run.Errors);
                }
            }
        }

        return failures.Count == 0 ? Results.Ok : Result.Fail(failures);
    }

    /// <summary>Where one seed's output goes. The same shape the campaign uses, so a gate's leftovers can be read by the same tools.</summary>
    public static string Directory(string root, int seed) =>
        Path.Combine(root, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"seed-{seed}"));
}
