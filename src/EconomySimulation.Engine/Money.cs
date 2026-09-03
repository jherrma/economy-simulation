using System.Globalization;

namespace EconomySimulation.Engine;

/// <summary>
/// An amount of money, in integer cents.
///
/// The reason for integer cents is V1. <c>Σ cash + pool == M0 + loans</c> has to hold to the
/// cent, and a floating-point representation makes that assertion either false or fuzzy — and a
/// fuzzy conservation check is worth almost nothing, because the bugs it exists to catch are
/// usually small.
///
/// There is deliberately no conversion from <see cref="double"/> or <see cref="decimal"/>.
/// Turning a computed fraction into money is a rounding decision, and every rounding decision in
/// this engine goes through <see cref="Scaled"/>, <see cref="Split"/> or <see cref="Allocate"/>
/// so that there is one place to look when a cent goes missing.
///
/// There is one currency and no rounding policy to choose beyond the ones named here.
/// </summary>
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    /// <summary>Whole euros. The only convenient constructor, and it takes an integer.</summary>
    public static Money FromEuros(long euros) => new(checked(euros * 100L));

    /// <summary>
    /// Euros from a configuration file, exactly.
    ///
    /// This is a boundary conversion, not an arithmetic one: <see cref="decimal"/> represents the
    /// two-decimal amounts a person writes in a TOML file without error, so nothing is rounded
    /// away that was ever there. It is deliberately not a conversion operator, and there is still
    /// no way in from <see cref="double"/> — a computed fraction has to go through
    /// <see cref="Scaled"/>, where the rounding is visible.
    /// </summary>
    public static Money FromEuros(decimal euros)
    {
        var cents = euros * 100m;

        if (cents != decimal.Truncate(cents))
        {
            throw new ArgumentOutOfRangeException(
                nameof(euros),
                euros,
                "An amount of money has at most two decimal places; there is no fraction of a cent.");
        }

        return new Money(checked((long)cents));
    }

    public bool IsZero => Cents == 0;

    public bool IsNegative => Cents < 0;

    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));

    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));

    public static Money operator -(Money a) => new(checked(-a.Cents));

    public static Money operator *(Money a, long n) => new(checked(a.Cents * n));

    public static Money operator *(long n, Money a) => a * n;

    public static bool operator <(Money a, Money b) => a.Cents < b.Cents;

    public static bool operator >(Money a, Money b) => a.Cents > b.Cents;

    public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;

    public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    /// <summary>
    /// Multiplies by a ratio — a price multiplier, a finance multiplier, a share — and rounds to
    /// the nearest cent, halves away from zero.
    ///
    /// This is the only place in the engine where a fractional amount of money becomes a whole
    /// one. Rounding half away from zero rather than to even is the commercial convention and is
    /// the less surprising of the two to read; nothing in V1 depends on the choice, because every
    /// rounded amount is *moved* by a transfer rather than computed independently on both sides.
    /// The moment an amount is computed twice and expected to match, use <see cref="Allocate"/>
    /// instead.
    /// </summary>
    public Money Scaled(double ratio)
    {
        var exact = Cents * ratio;

        // A checked conversion from double rejects NaN, infinity and out-of-range values rather
        // than producing long.MinValue, which would otherwise appear downstream as an enormous
        // and perfectly plausible-looking balance.
        return new Money(checked((long)Math.Round(exact, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// Divides into <paramref name="parts"/> equal shares that sum exactly to this amount, giving
    /// the remainder out one cent at a time to the earliest parts.
    ///
    /// €1.00 into three is 34, 33, 33 — never three times 33 with a cent unaccounted for.
    /// </summary>
    public Money[] Split(int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parts, 1);

        var share = Math.DivRem(Cents, parts, out var remainder);
        var step = Cents < 0 ? -1L : 1L;
        var extra = Math.Abs(remainder);

        var result = new Money[parts];
        for (var i = 0; i < parts; i++)
        {
            result[i] = new Money(i < extra ? share + step : share);
        }

        return result;
    }

    /// <summary>
    /// Divides this amount in the given proportions so that the parts sum exactly to it.
    ///
    /// Each part is floored, and the cents left over go to the parts with the largest discarded
    /// fractions, earliest first. This is what makes tier increments exact: a household that buys
    /// budget and then upgrades pays two amounts that add up to the standard price, with no
    /// residual cent appearing or vanishing. Rounding each share on its own is off by a cent for
    /// the prices that happen to be awkward, which shows up as V1 failing intermittently.
    /// </summary>
    public Money[] Allocate(ReadOnlySpan<double> weights)
    {
        if (weights.Length == 0)
        {
            throw new ArgumentException("Allocate needs at least one weight.", nameof(weights));
        }

        var total = 0.0;
        foreach (var w in weights)
        {
            if (w < 0 || double.IsNaN(w))
            {
                throw new ArgumentException(
                    $"Allocate needs non-negative weights, got {w.ToString(CultureInfo.InvariantCulture)}.",
                    nameof(weights));
            }

            total += w;
        }

        if (total <= 0)
        {
            throw new ArgumentException("Allocate needs the weights to sum to more than zero.", nameof(weights));
        }

        var result = new Money[weights.Length];
        var fractions = new double[weights.Length];
        var assigned = 0L;

        for (var i = 0; i < weights.Length; i++)
        {
            var exact = Cents * (weights[i] / total);
            var whole = (long)Math.Truncate(exact);
            fractions[i] = Math.Abs(exact - whole);
            result[i] = new Money(whole);
            assigned = checked(assigned + whole);
        }

        // Truncation always leaves something over in the direction of the total's own sign.
        var step = Cents < 0 ? -1L : 1L;
        var leftover = Math.Abs(Cents - assigned);

        for (var n = 0L; n < leftover; n++)
        {
            var best = -1;
            var bestFraction = double.NegativeInfinity;

            for (var i = 0; i < fractions.Length; i++)
            {
                if (fractions[i] > bestFraction)
                {
                    bestFraction = fractions[i];
                    best = i;
                }
            }

            result[best] = new Money(result[best].Cents + step);
            fractions[best] = double.NegativeInfinity;
        }

        return result;
    }

    /// <summary>
    /// The CSV form: invariant culture, always two decimals, no thousands separator and no
    /// currency symbol. A run written on a machine with a comma decimal separator has to be
    /// byte-identical to one written anywhere else, or V2 is measuring the locale.
    /// </summary>
    public string ToCsv() => ((decimal)Cents / 100m).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Deliberately not the CSV form. An amount interpolated into an output line by accident
    /// should be visible in the file rather than pass for a number.
    /// </summary>
    public override string ToString() => ToCsv() + " EUR";
}
