using static System.FormattableString;

namespace EconomySimulation.Gates;

/// <summary>
/// A scratch directory a gate runs into, removed when it is done with it.
///
/// Gates write whole runs and then throw them away: what is being kept is the comparison, not the
/// output. Keeping it is available (`--keep`) because a failed comparison is much easier to argue
/// with when both runs are still on disk.
/// </summary>
public sealed class Workspace : IDisposable
{
    private Workspace(string root, bool keep)
    {
        Root = root;
        Keep = keep;
    }

    public string Root { get; }

    public bool Keep { get; }

    public static Workspace Create(string gate, bool keep = false)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            Invariant($"economy-simulation-gate-{gate}-{Environment.ProcessId}-{DateTime.UtcNow:yyyyMMddHHmmss}"));

        Directory.CreateDirectory(root);

        return new Workspace(root, keep);
    }

    /// <summary>A named subdirectory — one arm of a comparison.</summary>
    public string Arm(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);

        return path;
    }

    public void Dispose()
    {
        if (Keep)
        {
            return;
        }

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // A scratch directory that could not be removed is not a gate result. Say so once and
            // carry on: turning a cleanup problem into a failed gate would make the gate report
            // the filesystem rather than the model.
            Console.Error.WriteLine(Invariant($"gates: could not remove {Root}: {failure.Message}"));
        }
    }
}
