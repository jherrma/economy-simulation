using FluentResults;

namespace EconomySimulation.Engine;

/// <summary>
/// The one success value the engine hands back.
///
/// `Result.Ok()` allocates — a result object and its reasons list — and the tick returns one per
/// step, per transfer, per household. That is thousands of allocations in a loop the specification
/// requires to allocate nothing (04-05), so success is a single shared instance. Sharing is safe
/// only while nobody mutates it, and FluentResults' `With…` methods do exactly that; ResultTests
/// scans the engine for them.
/// </summary>
public static class Results
{
    public static Result Ok { get; } = Result.Ok();

    /// <summary>
    /// Whether a result succeeded, without allocating. FluentResults answers `IsFailed` by
    /// running LINQ over the reasons list on every call — an enumerator per check, a quarter of a
    /// megabyte per tick. The shared instance is recognised by reference; anything else is asked
    /// the slow way, which only ever happens on a failure path.
    /// </summary>
    public static bool IsOk(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return ReferenceEquals(result, Ok) || result.IsSuccess;
    }
}
