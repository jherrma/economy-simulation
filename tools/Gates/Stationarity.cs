namespace EconomySimulation.Gates;

/// <summary>
/// Whether a series has settled, and when.
///
/// Two numbers, because "stationary" needs both and either alone passes something that is plainly
/// not settled. <see cref="Drift"/> catches a series still going somewhere; <see cref="Band"/>
/// catches one that is going nowhere loudly — a price rule with `k` set high enough to oscillate
/// has no trend at all, and a trend test alone would call it flat.
///
/// Both are relative to the series' own mean, so a price of one euro sixty and a price of sixteen
/// hundred are held to the same standard.
/// </summary>
public static class Stationarity
{
    /// <summary>
    /// How far the least-squares line moves across the window, relative to the mean.
    ///
    /// The total movement rather than the slope: a slope per tick is a number nobody can read
    /// against a tolerance, and what matters is whether the series went anywhere over the window
    /// that the results are averaged over.
    /// </summary>
    public static double Drift(ReadOnlySpan<double> values)
    {
        if (values.Length < 3)
        {
            return 0.0;
        }

        var n = values.Length;
        var meanX = (n - 1) / 2.0;
        var mean = Mean(values);
        var covariance = 0.0;
        var variance = 0.0;

        for (var i = 0; i < n; i++)
        {
            var dx = i - meanX;

            covariance += dx * (values[i] - mean);
            variance += dx * dx;
        }

        if (variance == 0.0 || mean == 0.0)
        {
            return 0.0;
        }

        return covariance / variance * (n - 1) / Math.Abs(mean);
    }

    /// <summary>The peak-to-trough spread over the window, relative to the mean.</summary>
    public static double Band(ReadOnlySpan<double> values)
    {
        if (values.IsEmpty)
        {
            return 0.0;
        }

        var mean = Mean(values);

        if (mean == 0.0)
        {
            return 0.0;
        }

        var low = values[0];
        var high = values[0];

        foreach (var value in values)
        {
            low = Math.Min(low, value);
            high = Math.Max(high, value);
        }

        return (high - low) / Math.Abs(mean);
    }

    public static double Mean(ReadOnlySpan<double> values)
    {
        if (values.IsEmpty)
        {
            return 0.0;
        }

        var total = 0.0;

        foreach (var value in values)
        {
            total += value;
        }

        return total / values.Length;
    }

    /// <summary>
    /// The earliest tick from which the rest of the run is flat, or zero if no such tick exists.
    ///
    /// This is what turns `warmup_ticks` from a guess into an observation. A gate that answered only
    /// pass or fail would leave the parameter unexamined: a mix that settles at tick 200 and a mix
    /// that settled at tick 12 both pass a test taken over ticks 121 onwards, and only one of them
    /// means the warm-up is long enough.
    /// </summary>
    /// <param name="values">The whole run, from tick 1.</param>
    /// <param name="minimumWindow">Shorter windows are flat for arithmetic rather than economic reasons.</param>
    public static int SettlesAt(ReadOnlySpan<double> values, double drift, double band, int minimumWindow = 60)
    {
        for (var start = 0; start + minimumWindow <= values.Length; start++)
        {
            var window = values[start..];

            if (Math.Abs(Drift(window)) <= drift && Band(window) <= band)
            {
                return start + 1;
            }
        }

        return 0;
    }
}
