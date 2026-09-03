namespace EconomySimulation.Engine;

/// <summary>
/// One named stream of pseudo-random numbers.
///
/// The generator is **xoshiro256++**, seeded through **SplitMix64**, both written out here. It is
/// pinned rather than taken from <see cref="System.Random"/> on purpose: the framework's shared
/// implementation is explicitly documented as free to change between releases, and a run of this
/// model has to be reproducible in five years by someone checking the result, not only next
/// Tuesday on this machine.
///
/// A class rather than a mutable struct. Streams are opened once — per household, per purpose, at
/// setup — so the allocation is not in any hot loop, and a mutable struct copied by accident into
/// a <c>foreach</c> variable or a lambda would advance a copy and silently produce a *different*
/// but still perfectly deterministic run. That is the worst possible failure for this project,
/// because every check it has would stay green.
/// </summary>
public sealed class RandomStream
{
    private ulong s0;
    private ulong s1;
    private ulong s2;
    private ulong s3;

    private RandomStream(ulong seed)
    {
        // SplitMix64 expansion: a single seed word into four state words, so that adjacent seeds
        // do not produce correlated streams.
        s0 = SplitMix64(ref seed);
        s1 = SplitMix64(ref seed);
        s2 = SplitMix64(ref seed);
        s3 = SplitMix64(ref seed);

        if ((s0 | s1 | s2 | s3) == 0)
        {
            s0 = 1;
        }
    }

    /// <summary>
    /// The stream a household draws from for one purpose: <c>hash(run_seed, household_id,
    /// purpose)</c>.
    /// </summary>
    public static RandomStream ForHousehold(int runSeed, int householdId, Purpose purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentOutOfRangeException.ThrowIfNegative(householdId);

        return new RandomStream(Derive(runSeed, purpose.Hash, householdId, HouseholdDomain));
    }

    /// <summary>The stream a tick draws from for one purpose — the shopping order, in v1.</summary>
    public static RandomStream ForTick(int runSeed, int tick, Purpose purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        return new RandomStream(Derive(runSeed, purpose.Hash, tick, TickDomain));
    }

    /// <summary>A raw 64-bit draw.</summary>
    public ulong NextUInt64()
    {
        unchecked
        {
            var result = RotateLeft(s0 + s3, 23) + s0;
            var t = s1 << 17;

            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = RotateLeft(s3, 45);

            return result;
        }
    }

    /// <summary>A draw in [0, 1), with 53 bits of resolution — every bit a double can hold.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// A draw in [0, <paramref name="exclusiveUpper"/>), with no modulo bias.
    ///
    /// Lemire's multiply-and-reject. The rejection branch is what makes it unbiased, and it is
    /// also why this is not simply <c>NextUInt64() % n</c>: for a shuffle over a thousand
    /// households the bias would be small, invisible, and permanent.
    /// </summary>
    public int NextInt(int exclusiveUpper)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(exclusiveUpper, 1);

        var range = (ulong)exclusiveUpper;

        unchecked
        {
            var product = (UInt128)NextUInt64() * range;
            var low = (ulong)product;

            if (low < range)
            {
                var threshold = (0UL - range) % range;

                while (low < threshold)
                {
                    product = (UInt128)NextUInt64() * range;
                    low = (ulong)product;
                }
            }

            return (int)(ulong)(product >> 64);
        }
    }

    /// <summary>
    /// A draw from the standard normal, by Box-Muller.
    ///
    /// Box-Muller produces two independent variates at a time and this returns one, discarding
    /// the other. Caching the spare would save a transform and make the stream's output depend on
    /// how many normals had been asked for earlier — still deterministic, but deterministic in a
    /// way that couples one caller to another. Nothing in this model is short of random numbers.
    /// </summary>
    public double NextStandardNormal()
    {
        // NextDouble is in [0, 1); log(0) is not a number this model can use.
        double uniform;
        do
        {
            uniform = NextDouble();
        }
        while (uniform <= 0.0);

        return Math.Sqrt(-2.0 * Math.Log(uniform)) * Math.Cos(2.0 * Math.PI * NextDouble());
    }

    /// <summary>
    /// A draw from the lognormal with the given mean and log-spread.
    ///
    /// The `− σ²/2` is the whole point: without it the mean of the draws is `mean · exp(σ²/2)`,
    /// which at σ = 0.35 is 6.3 per cent too high. That is not a rounding error — it is the town
    /// earning six per cent more than the goods table was calibrated to supply, and the symptom
    /// is a pool that drains and a model that looks like it has an inflation problem.
    /// </summary>
    public double NextLogNormal(double mean, double sigma) =>
        mean * Math.Exp((sigma * NextStandardNormal()) - (sigma * sigma / 2.0));

    /// <summary>
    /// Fisher-Yates, in place. Used for the one thing in this model that is legitimately
    /// order-dependent: who shops first, and therefore who gets the last unit.
    /// </summary>
    public void Shuffle<T>(Span<T> items)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    // ---- deriving a seed -----------------------------------------------------------------

    /// <summary>
    /// Distinguishes a household-keyed stream from a tick-keyed one, so that household 5 and
    /// tick 5 cannot collide even if a purpose is ever used for both.
    /// </summary>
    private const ulong HouseholdDomain = 0x486F7573_65686F6CUL;

    private const ulong TickDomain = 0x5469636B_5469636BUL;

    private static ulong Derive(int runSeed, ulong purposeHash, long key, ulong domain)
    {
        unchecked
        {
            // Mix each component in turn rather than adding them: the point is that changing any
            // one of the three moves the whole stream, and that no two combinations meet.
            var state = domain;

            state ^= (ulong)(uint)runSeed * 0x9E3779B97F4A7C15UL;
            state = SplitMix64(ref state);

            state ^= purposeHash;
            state = SplitMix64(ref state);

            state ^= (ulong)key * 0xBF58476D1CE4E5B9UL;

            return SplitMix64(ref state);
        }
    }

    private static ulong SplitMix64(ref ulong state)
    {
        unchecked
        {
            // unchecked throughout: this project builds with CheckForOverflowUnderflow, which is
            // right for a balance sheet and wrong for a mixing function, where the wrap-around
            // *is* the arithmetic.
            state += 0x9E3779B97F4A7C15UL;

            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

            return z ^ (z >> 31);
        }
    }

    private static ulong RotateLeft(ulong x, int k)
    {
        unchecked
        {
            return (x << k) | (x >> (64 - k));
        }
    }
}
