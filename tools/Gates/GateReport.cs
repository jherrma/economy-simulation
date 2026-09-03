using System.Text;

namespace EconomySimulation.Gates;

/// <summary>
/// What one gate found: a list of named checks, each of which either passed or has reasons it did
/// not, and each of which may carry observations whether it passed or failed.
///
/// **Observations are the point, not decoration.** A gate that answers only pass or fail can be
/// satisfied by loosening it, and nobody reading a green run learns anything. The null run reports
/// the tick each series settled at so that `warmup_ticks` is set from evidence; the neutrality gate
/// reports the largest cent discrepancy it saw so that the rounding bound is a measurement rather
/// than a claim. Both are useful on a passing run and neither can be produced afterwards.
/// </summary>
public sealed class GateReport(string name)
{
    private readonly List<GateCheck> checks = [];

    public string Name { get; } = name;

    public IReadOnlyList<GateCheck> Checks => checks;

    public bool Passed => checks.Count > 0 && checks.TrueForAll(c => c.Passed);

    /// <summary>Adds a check and returns it, so a gate reads as a list of named claims.</summary>
    public GateCheck Check(string name)
    {
        var check = new GateCheck(name);
        checks.Add(check);

        return check;
    }

    /// <summary>
    /// The report as text. Deliberately plain: this is read in a terminal and in a CI log, and a
    /// failure has to be legible in both without a viewer.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder();

        text.Append(Passed ? "PASS  " : "FAIL  ").AppendLine(Name);

        foreach (var check in checks)
        {
            text.Append("  ").Append(check.Passed ? "ok    " : "FAILED").Append(' ').AppendLine(check.Name);

            foreach (var observation in check.Observations)
            {
                text.Append("           ").AppendLine(observation);
            }

            foreach (var failure in check.Failures)
            {
                text.Append("    ---> ").AppendLine(failure);
            }
        }

        return text.ToString();
    }
}

/// <summary>One named claim a gate makes, and everything it saw while checking it.</summary>
public sealed class GateCheck(string name)
{
    private readonly List<string> failures = [];
    private readonly List<string> observations = [];

    public string Name { get; } = name;

    public bool Passed => failures.Count == 0;

    public IReadOnlyList<string> Failures => failures;

    public IReadOnlyList<string> Observations => observations;

    /// <summary>A reason this check did not hold. The first one is usually the only one worth reading, so put the location in it.</summary>
    public void Fail(string reason) => failures.Add(reason);

    /// <summary>Something measured. Reported whether or not the check passed.</summary>
    public void Observe(string what) => observations.Add(what);
}
