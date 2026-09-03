namespace EconomySimulation.Runner;

/// <summary>
/// Parses arguments, calls the engine, reports where the output went. No model logic lives here,
/// and no analysis: the engine writes CSV and nothing else.
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        Console.Error.WriteLine("economy-simulation: no scenario runner yet (spec/stories/09-02).");
        return args.Length == 0 ? 0 : 1;
    }
}
