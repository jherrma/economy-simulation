using System.Security.Cryptography;
using System.Text;
using EconomySimulation.Engine;
using EconomySimulation.Tests.Infrastructure;

namespace EconomySimulation.Tests;

/// <summary>
/// V2, at the level of the streams themselves. See spec/stories/01-05.
///
/// The file-level version of these checks — two whole runs, byte-compared — is 08-01. What is
/// asserted here is the property the file-level check will rest on: that a stream depends on the
/// seed, the household and the purpose, and on nothing else in the program.
/// </summary>
public sealed class DeterminismTests
{
    private const int Seed = 12345;

    private static readonly Purpose[] HouseholdPurposes =
    [
        Purpose.Income,
        Purpose.Willingness,
        Purpose.Theta,
        Purpose.Abstainer,
        Purpose.InitialAge,
        Purpose.Finance,
    ];

    // ---- the generator is pinned, not inherited -----------------------------------------

    /// <summary>
    /// The first draws of a known stream, written down.
    ///
    /// This is the test that fails when someone swaps the generator for System.Random or "tidies"
    /// the mixing function. Nothing else would notice: the model would still be perfectly
    /// deterministic, still pass every other check, and no longer be the same experiment.
    /// </summary>
    [Fact]
    public void TheGeneratorProducesTheSameNumbersItAlwaysHas()
    {
        var stream = RandomStream.ForHousehold(Seed, 7, Purpose.Income);

        Assert.Equal(
            [
                14959717979141612568UL,
                15452103597286435792UL,
                16689343029958923803UL,
                4316235967373557620UL,
            ],
            Enumerable.Range(0, 4).Select(_ => stream.NextUInt64()));
    }

    [Fact]
    public void TheTickStreamsArePinnedToo()
    {
        var stream = RandomStream.ForTick(Seed, 7, Purpose.Order);

        Assert.Equal(
            [3802129142807523932UL, 467221828184002519UL],
            Enumerable.Range(0, 2).Select(_ => stream.NextUInt64()));
    }

    [Fact]
    public void DoublesArePinnedToo()
    {
        var stream = RandomStream.ForHousehold(1, 0, Purpose.Theta);

        Assert.Equal(
            [0.8403166373877442, 0.38935402703019173, 0.806093658068215],
            Enumerable.Range(0, 3).Select(_ => stream.NextDouble()));
    }

    /// <summary>
    /// The purpose hash is written out rather than taken from string.GetHashCode, which is
    /// randomised per process. Had it not been, every run of this model would be irreproducible
    /// across processes and nothing in the program would have said so.
    /// </summary>
    [Fact]
    public void ThePurposeHashSurvivesARestart()
    {
        var first = RandomStream.ForHousehold(Seed, 0, Purpose.Income).NextUInt64();
        var again = RandomStream.ForHousehold(Seed, 0, Purpose.Register("income-probe-1")).NextUInt64();

        // Same seed, same household, different purpose: a different stream, deterministically so.
        Assert.NotEqual(first, again);
        Assert.Equal(first, RandomStream.ForHousehold(Seed, 0, Purpose.Income).NextUInt64());
    }

    // ---- what a stream depends on -------------------------------------------------------

    [Fact]
    public void TheSameSeedGivesTheSameSequence()
    {
        Assert.Equal(Digest(Seed), Digest(Seed));
    }

    [Fact]
    public void ADifferentSeedGivesADifferentSequence()
    {
        Assert.NotEqual(Digest(Seed), Digest(Seed + 1));
    }

    [Fact]
    public void EveryHouseholdAndPurposeCombinationIsItsOwnStream()
    {
        var seen = new HashSet<ulong>();

        for (var household = 0; household < 200; household++)
        {
            foreach (var purpose in HouseholdPurposes)
            {
                Assert.True(
                    seen.Add(RandomStream.ForHousehold(Seed, household, purpose).NextUInt64()),
                    $"Household {household}/{purpose} collided with an earlier stream.");
            }
        }
    }

    /// <summary>
    /// Household 5 and tick 5 are keyed by the same number. They are kept apart by a domain tag
    /// rather than by nobody having reused a purpose yet.
    /// </summary>
    [Fact]
    public void AHouseholdKeyAndATickKeyDoNotCollide()
    {
        Assert.NotEqual(
            RandomStream.ForHousehold(Seed, 5, Purpose.Order).NextUInt64(),
            RandomStream.ForTick(Seed, 5, Purpose.Order).NextUInt64());
    }

    // ---- the two properties the story is really about -----------------------------------

    /// <summary>
    /// Registering a new purpose and drawing nothing from it changes nothing.
    ///
    /// This is the test that pays for the whole design. Every mechanism added after v1 arrives
    /// behind a switch whose *off* setting has to reproduce the previous version byte for byte,
    /// and that is only possible if adding the mechanism's randomness does not move anything
    /// else's. With numbered or sliced streams this test cannot be made to pass.
    /// </summary>
    [Fact]
    public void RegisteringANewPurposeAndDrawingNothingChangesNothing()
    {
        var before = Digest(Seed);

        var unused = Purpose.Register("supply-response-probe");
        Assert.NotNull(unused);

        Assert.Equal(before, Digest(Seed));
    }

    /// <summary>
    /// Serial and parallel give the same answer, because there is nothing shared to race over.
    /// A single generator handed out to threads would pass every other test in this file.
    /// </summary>
    [Fact]
    public void SerialAndParallelAgree()
    {
        const int households = 400;

        var serial = new ulong[households];
        for (var h = 0; h < households; h++)
        {
            serial[h] = Sweep(Seed, h);
        }

        var parallel = new ulong[households];
        Parallel.For(0, households, h => parallel[h] = Sweep(Seed, h));

        Assert.Equal(serial, parallel);
    }

    // ---- no shared generator anywhere ---------------------------------------------------

    /// <summary>
    /// The registry is worth nothing if a single <c>Random</c> is sitting beside it. This is the
    /// only way to check that, because the symptom of a shared generator is a run that varies
    /// with thread scheduling — which is to say, intermittently, on someone else's machine.
    /// </summary>
    [Fact]
    public void TheEngineHasNoSharedGenerator()
    {
        string[] banned = ["new Random(", "Random.Shared", "RandomNumberGenerator", "Guid.NewGuid"];

        var offenders = new List<string>();

        foreach (var path in Repo.EngineSources())
        {
            var text = File.ReadAllText(path);

            offenders.AddRange(
                banned
                    .Where(b => text.Contains(b, StringComparison.Ordinal))
                    .Select(b => $"{Path.GetFileName(path)}: {b}"));
        }

        Assert.True(offenders.Count == 0, "Shared randomness in the engine: " + string.Join("; ", offenders));
    }

    [Fact]
    public void APurposeCannotBeRegisteredTwice()
    {
        Purpose.Register("duplicate-probe");

        var thrown = Assert.Throws<ArgumentException>(() => Purpose.Register("duplicate-probe"));
        Assert.Contains("already registered", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheV1PurposesAreAllRegistered()
    {
        var names = Purpose.All.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in
                 new[] { "income", "willingness", "theta", "abstainer", "initial_age", "finance", "order" })
        {
            Assert.Contains(expected, names);
        }
    }

    // ---- the draws themselves -----------------------------------------------------------

    [Fact]
    public void NextDouble_StaysInRange()
    {
        var stream = RandomStream.ForHousehold(Seed, 3, Purpose.Willingness);

        for (var i = 0; i < 100_000; i++)
        {
            var d = stream.NextDouble();
            Assert.InRange(d, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void NextInt_StaysInRange_AndCoversIt()
    {
        var stream = RandomStream.ForHousehold(Seed, 4, Purpose.Willingness);
        var seen = new bool[7];

        for (var i = 0; i < 10_000; i++)
        {
            var n = stream.NextInt(7);
            Assert.InRange(n, 0, 6);
            seen[n] = true;
        }

        Assert.DoesNotContain(false, seen);
    }

    [Fact]
    public void NextInt_OfOne_IsAlwaysZero()
    {
        var stream = RandomStream.ForHousehold(Seed, 5, Purpose.Willingness);

        Assert.Equal(0, stream.NextInt(1));
    }

    [Fact]
    public void Shuffle_IsAPermutation()
    {
        var stream = RandomStream.ForTick(Seed, 1, Purpose.Order);
        var items = Enumerable.Range(0, 1000).ToArray();

        stream.Shuffle(items.AsSpan());

        Assert.Equal(Enumerable.Range(0, 1000), items.Order());
    }

    [Fact]
    public void Shuffle_IsRedrawnEveryTick()
    {
        var first = Order(tick: 1);
        var second = Order(tick: 2);

        Assert.NotEqual(first, second);
        Assert.Equal(first, Order(tick: 1));

        static int[] Order(int tick)
        {
            var items = Enumerable.Range(0, 50).ToArray();
            RandomStream.ForTick(Seed, tick, Purpose.Order).Shuffle(items.AsSpan());
            return items;
        }
    }

    // -------------------------------------------------------------------------------------

    /// <summary>Every stream the v1 model opens for one household, drawn from and folded up.</summary>
    private static ulong Sweep(int seed, int household)
    {
        var mixed = 1469598103934665603UL;

        foreach (var purpose in HouseholdPurposes)
        {
            var stream = RandomStream.ForHousehold(seed, household, purpose);

            for (var i = 0; i < 8; i++)
            {
                unchecked
                {
                    mixed = (mixed ^ stream.NextUInt64()) * 1099511628211UL;
                }
            }
        }

        return mixed;
    }

    /// <summary>
    /// A stand-in for an output file: what the whole model's randomness looks like for one seed.
    /// 08-01 does this with the real files.
    /// </summary>
    private static string Digest(int seed)
    {
        var text = new StringBuilder();

        for (var household = 0; household < 100; household++)
        {
            text.Append(Sweep(seed, household)).Append('\n');
        }

        for (var tick = 0; tick < 12; tick++)
        {
            var items = Enumerable.Range(0, 20).ToArray();
            RandomStream.ForTick(seed, tick, Purpose.Order).Shuffle(items.AsSpan());
            text.Append(string.Join(',', items)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
