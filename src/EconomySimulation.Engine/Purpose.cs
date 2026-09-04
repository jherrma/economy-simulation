using System.Collections.Concurrent;
using System.Text;

namespace EconomySimulation.Engine;

/// <summary>
/// What a random stream is *for*. Streams are keyed by purpose, not by position, and this is the
/// one registry of them.
///
/// The reason is not tidiness. Every mechanism added after v1 — a supply response, wages, default,
/// a second-hand market — will want randomness. If adding one shifts the draws the current model
/// makes, then the new version and the old are running on different random worlds, and the
/// difference between their results is noise plus mechanism with no way to separate the two. That
/// difference is the project's entire measuring instrument (V5), so it has to survive the model
/// growing.
///
/// Naming a stream by purpose makes that impossible by construction. Numbering it, or slicing it
/// out of a shared generator, makes the opposite inevitable.
/// </summary>
public sealed class Purpose
{
    private static readonly ConcurrentDictionary<string, Purpose> Registry = new(StringComparer.Ordinal);

    // ---- v1 ----------------------------------------------------------------------------

    /// <summary>Per household, once: the lognormal income draw.</summary>
    public static readonly Purpose Income = Register("income");

    /// <summary>Per household, once: the taste weight w_h.</summary>
    public static readonly Purpose Willingness = Register("willingness");

    /// <summary>Per household, once: θ_h, the propensity to reach for credit.</summary>
    public static readonly Purpose Theta = Register("theta");

    /// <summary>Per household, once: whether this household is one of the fifth who never borrow.</summary>
    public static readonly Purpose Abstainer = Register("abstainer");

    /// <summary>
    /// Per household, once: how far through its life each durable already is at tick 0.
    /// Without this the whole town replaces its appliances in the same month, forever.
    /// </summary>
    public static readonly Purpose InitialAge = Register("initial_age");

    /// <summary>Per household, per candidate: the θ_h coin flip inside the walk.</summary>
    public static readonly Purpose Finance = Register("finance");

    // ---- E10 -----------------------------------------------------------------------------

    /// <summary>
    /// Per household, once: which archetype it is (`01-SIMULATION.md` §5.4).
    ///
    /// Drawn in **every** run, including under the identity table where there is one type and the
    /// answer is always the same. Skipping it there is the natural optimisation and it is the one
    /// that quietly ends the experiment: a typed scenario and its own `credit_off` would then sit
    /// on different random worlds. Same argument as θ's, and it costs one draw per household.
    /// </summary>
    public static readonly Purpose Archetype = Register("archetype");

    /// <summary>
    /// Per household, per category: `ε_h,g`, the residual on top of the archetype's taste.
    ///
    /// At the default `sigma_idio = 0` it is exactly 1 and changes nothing — but the draw still
    /// happens, for the same reason θ's does.
    /// </summary>
    public static readonly Purpose TasteIdiosyncratic = Register("taste_idio");

    /// <summary>
    /// Per household, per good, per tick: whether the unit held fails (`01-SIMULATION.md` §5.5).
    ///
    /// **The draw is unconditional** — every household, every good, every tick, whether or not a
    /// unit is owned. A household with nothing cannot lose anything, so drawing for it looks like
    /// waste, and skipping it is the bug: the stream position would then depend on who was
    /// rationed, which differs between arms *by construction*, since being rationed is the thing
    /// under measurement. The two arms would sit on different worlds, every household would be a
    /// different household, and the paired comparison would keep returning plausible numbers.
    ///
    /// θ had this argument in 02-04 and the archetype assignment had it in 10-02. This is its third
    /// and least obvious instance, because here the *state* that would gate the draw is itself an
    /// outcome.
    /// </summary>
    public static readonly Purpose Failure = Register("failure");

    /// <summary>
    /// Per tick: the order households shop in.
    ///
    /// This is the one place the model legitimately wants order dependence — under rationing, the
    /// order decides who gets the last unit. A shuffle driven by its own stream is a different
    /// thing from letting processing order leak into every draw in the model.
    /// </summary>
    public static readonly Purpose Order = Register("order");

    // -------------------------------------------------------------------------------------

    private Purpose(string name)
    {
        Name = name;
        Hash = Fnv1a64(name);
    }

    public string Name { get; }

    /// <summary>
    /// The purpose's contribution to a stream's seed. FNV-1a over the UTF-8 name, written out
    /// here rather than taken from <c>string.GetHashCode</c>, which is randomised per process and
    /// would make every run of this model irreproducible in a way nothing would report.
    /// </summary>
    internal ulong Hash { get; }

    /// <summary>
    /// Adds a purpose. Names are unique, and a duplicate is a mistake rather than an alias: two
    /// mechanisms sharing a stream is exactly what the registry exists to prevent.
    /// </summary>
    public static Purpose Register(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var purpose = new Purpose(name);

        if (!Registry.TryAdd(name, purpose))
        {
            throw new ArgumentException(
                $"The purpose '{name}' is already registered. Two mechanisms must not share a "
                + "stream; give the new one its own name.",
                nameof(name));
        }

        return purpose;
    }

    /// <summary>Every registered purpose, in name order — for reporting, not for keying.</summary>
    public static IReadOnlyList<Purpose> All =>
        [.. Registry.Values.OrderBy(p => p.Name, StringComparer.Ordinal)];

    public override string ToString() => Name;

    private static ulong Fnv1a64(string text)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;

        // unchecked, deliberately: the whole project builds with CheckForOverflowUnderflow, which
        // is right for balances and wrong for a hash, where wrapping is the arithmetic.
        unchecked
        {
            foreach (var b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= prime;
            }
        }

        return hash;
    }
}
