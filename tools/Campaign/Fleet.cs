using System.Diagnostics;
using FluentResults;
using static System.FormattableString;

namespace EconomySimulation.Campaign;

/// <summary>
/// The 150 runs, one process each.
///
/// A process rather than a thread because a run that halts on V1 must take nothing else with it,
/// and because a campaign is the one place where "the engine, started fresh, from the commit this
/// manifest names" has to be literally true. The cost is a process start per run, which against a
/// six-hundred-tick simulation is noise.
/// </summary>
public static class Fleet
{
    public readonly record struct Unit(string Scenario, int Seed)
    {
        public override string ToString() => Invariant($"{Scenario} seed {Seed}");
    }

    public static IReadOnlyList<Unit> Units(IReadOnlyList<string> scenarios, IReadOnlyList<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(seeds);

        return [.. scenarios.SelectMany(scenario => seeds.Select(seed => new Unit(scenario, seed)))];
    }

    public static Result RunAll(IReadOnlyList<Unit> units, string output, Action<Unit>? done = null)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        var runner = Layout.Runner();
        var failures = new List<IError>();
        var complete = 0;

        Parallel.ForEach(
            units,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            unit =>
            {
                var ran = Spawn(runner, unit, Layout.Run(output, unit.Scenario, unit.Seed));

                lock (failures)
                {
                    if (ran.IsFailed)
                    {
                        failures.AddRange(ran.Errors);
                    }

                    complete++;
                    done?.Invoke(unit);
                }
            });

        _ = complete;

        return failures.Count > 0 ? Result.Fail(failures) : Result.Ok();
    }

    private static Result Spawn(string runner, Unit unit, string into)
    {
        var start = new ProcessStartInfo
        {
            FileName = runner.EndsWith(".dll", StringComparison.Ordinal) ? "dotnet" : runner,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        if (runner.EndsWith(".dll", StringComparison.Ordinal))
        {
            start.ArgumentList.Add(runner);
        }

        start.ArgumentList.Add("--scenario");
        start.ArgumentList.Add(unit.Scenario);
        start.ArgumentList.Add("--seed");
        start.ArgumentList.Add(unit.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--into");
        start.ArgumentList.Add(into);
        start.ArgumentList.Add("--scenarios");
        start.ArgumentList.Add(Layout.Scenarios);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {runner}.");

        // Read before waiting: a child that fills its error pipe while nobody drains it blocks
        // forever, and a campaign that hangs on its own diagnostics is worse than one that fails.
        var error = process.StandardError.ReadToEnd();
        _ = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? Result.Ok()
            : Result.Fail(Invariant($"{unit}: the run failed ({error.Trim()})"));
    }
}
