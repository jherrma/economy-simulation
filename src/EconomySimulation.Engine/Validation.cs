using System.Globalization;
using FluentResults;

namespace EconomySimulation.Engine;

/// <summary>
/// Collects everything wrong with a configuration before saying so.
///
/// The difference between this and returning on the first problem is the difference between a
/// loader that costs one edit and one that costs a dozen run-fix-rerun cycles. A parameter file
/// with three mistakes in it should report three mistakes.
///
/// Every failure names what was wrong, what was expected, and what was actually there. A bare
/// <c>Result.Fail("error")</c> is not acceptable here: the person reading it is looking at a run
/// that did not start, in a directory of thirty seeds, and the message is all they have.
/// </summary>
public sealed class Validation
{
    private readonly List<IError> errors = [];

    public bool HasFailures => errors.Count > 0;

    /// <summary>Records a problem, in the fixed subject / expected / actual shape.</summary>
    public Validation Fail(string subject, string expected, object? actual)
    {
        errors.Add(new Error(Describe(subject, expected, actual)));
        return this;
    }

    /// <summary>Records a problem unless <paramref name="condition"/> holds.</summary>
    public Validation Require(bool condition, string subject, string expected, object? actual) =>
        condition ? this : Fail(subject, expected, actual);

    /// <summary>Folds in the failures of another operation, keeping its messages.</summary>
    public Validation Absorb(ResultBase result)
    {
        if (result.IsFailed)
        {
            errors.AddRange(result.Errors);
        }

        return this;
    }

    /// <summary>Every problem found, as one failed <see cref="Result"/>, or success.</summary>
    public Result ToResult() => errors.Count == 0 ? Results.Ok : Result.Fail(errors);

    /// <summary>
    /// The validated value, or every problem found. <paramref name="value"/> is only called when
    /// nothing failed, so it may assume what was checked.
    /// </summary>
    public Result<T> ToResult<T>(Func<T> value) =>
        errors.Count == 0 ? Result.Ok(value()) : Result.Fail<T>(errors);

    private static string Describe(string subject, string expected, object? actual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(expected);

        var seen = actual switch
        {
            null => "nothing",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => actual.ToString() ?? "nothing",
        };

        return $"{subject}: expected {expected}, got {seen}";
    }
}
