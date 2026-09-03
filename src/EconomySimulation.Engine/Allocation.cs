using System.Globalization;

namespace EconomySimulation.Engine;

/// <summary>
/// Splitting a whole into parts that add back up to it.
///
/// Used for money and for units of supply, from one implementation, because both need the same
/// guarantee: rounding each share on its own leaves a residue, and in this model a residue is
/// either a cent that breaks V1 or a unit of supply that appears from nowhere.
/// </summary>
internal static class Allocation
{
    /// <summary>
    /// Divides <paramref name="total"/> in the given proportions, largest remainder first.
    ///
    /// Each part is truncated, and what is left over goes one at a time to the parts with the
    /// largest discarded fractions, earliest first when they tie. The parts sum to the total
    /// exactly, for any weights and any total.
    /// </summary>
    internal static long[] LargestRemainder(long total, ReadOnlySpan<double> weights)
    {
        if (weights.Length == 0)
        {
            throw new ArgumentException("An allocation needs at least one weight.", nameof(weights));
        }

        var weightTotal = 0.0;
        foreach (var weight in weights)
        {
            if (weight < 0 || double.IsNaN(weight))
            {
                throw new ArgumentException(
                    $"An allocation needs non-negative weights, got {weight.ToString(CultureInfo.InvariantCulture)}.",
                    nameof(weights));
            }

            weightTotal += weight;
        }

        if (weightTotal <= 0)
        {
            throw new ArgumentException(
                "An allocation needs the weights to sum to more than zero.",
                nameof(weights));
        }

        var parts = new long[weights.Length];
        var fractions = new double[weights.Length];
        var assigned = 0L;

        for (var i = 0; i < weights.Length; i++)
        {
            var exact = total * (weights[i] / weightTotal);
            var whole = (long)Math.Truncate(exact);

            parts[i] = whole;
            fractions[i] = Math.Abs(exact - whole);
            assigned = checked(assigned + whole);
        }

        // Truncation always leaves something over, in the direction of the total's own sign.
        var step = total < 0 ? -1L : 1L;
        var leftover = Math.Abs(total - assigned);

        for (var n = 0L; n < leftover; n++)
        {
            var best = 0;
            var bestFraction = double.NegativeInfinity;

            for (var i = 0; i < fractions.Length; i++)
            {
                if (fractions[i] > bestFraction)
                {
                    bestFraction = fractions[i];
                    best = i;
                }
            }

            parts[best] += step;
            fractions[best] = double.NegativeInfinity;
        }

        return parts;
    }
}
