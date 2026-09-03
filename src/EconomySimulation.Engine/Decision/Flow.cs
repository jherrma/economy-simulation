using System.Globalization;

namespace EconomySimulation.Engine.Decision;

/// <summary>
/// Euros per tick — the unit everything the household compares is expressed in.
///
/// A <see cref="Money"/> is a stock: an amount that sits in an account and moves to the cent. A
/// flow is a rate of valuation or of cost — what a unit is worth per tick, what it costs per tick —
/// and it is never held, moved or summed into a balance. That is why it may be a floating-point
/// number where money may not: no conservation identity runs through it, and the only thing ever
/// done with two flows is to divide one by the other, which yields a pure number.
///
/// There are no utility units anywhere in the engine. The floor and the income-linked part of a
/// good's value are both euros per tick (`02-PARAMETERS.md` §3.1), so a month of food and an
/// eight-year appliance are compared in the same currency the household pays in.
/// </summary>
public readonly record struct Flow(double EurosPerTick) : IComparable<Flow>
{
    public static readonly Flow Zero = new(0.0);

    /// <summary>
    /// An amount of money spread over a number of ticks: `price / life`, the cost side of the
    /// decision. A non-durable has `life = 1`, so its flow cost collapses to its price.
    /// </summary>
    public static Flow Spread(Money total, int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticks, 1);

        return new Flow(total.Cents / 100.0 / ticks);
    }

    public bool IsPositive => EurosPerTick > 0.0;

    public static Flow operator +(Flow a, Flow b) => new(a.EurosPerTick + b.EurosPerTick);

    public static Flow operator -(Flow a, Flow b) => new(a.EurosPerTick - b.EurosPerTick);

    /// <summary>Scales by a pure number — a tier's value multiplier, a taste weight.</summary>
    public static Flow operator *(Flow flow, double factor) => new(flow.EurosPerTick * factor);

    public static Flow operator *(double factor, Flow flow) => flow * factor;

    /// <summary>Two flows make a pure number. This is the only way a flow leaves the type.</summary>
    public static double operator /(Flow numerator, Flow denominator) =>
        numerator.EurosPerTick / denominator.EurosPerTick;

    public static bool operator <(Flow a, Flow b) => a.EurosPerTick < b.EurosPerTick;

    public static bool operator >(Flow a, Flow b) => a.EurosPerTick > b.EurosPerTick;

    public static bool operator <=(Flow a, Flow b) => a.EurosPerTick <= b.EurosPerTick;

    public static bool operator >=(Flow a, Flow b) => a.EurosPerTick >= b.EurosPerTick;

    public int CompareTo(Flow other) => EurosPerTick.CompareTo(other.EurosPerTick);

    public override string ToString() =>
        EurosPerTick.ToString("0.00", CultureInfo.InvariantCulture) + " EUR/tick";
}
