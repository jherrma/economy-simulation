using System.Globalization;
using Tomlyn.Model;

namespace EconomySimulation.Engine.Configuration;

/// <summary>
/// One table of a configuration file, read key by key, collecting everything wrong with it.
///
/// The absent-versus-unknown asymmetry is the whole design. **Absent** means an older file, or a
/// caller who does not care, and is filled from the default — which is what lets a scenario
/// written today still run against a model that has grown three mechanisms. **Unknown** means a
/// typo, or a parameter someone invented, and must fail: a misspelled key that is silently
/// ignored produces a run at defaults that looks exactly like the run that was asked for.
/// </summary>
internal sealed class TomlSection
{
    private readonly TomlTable table;
    private readonly string path;
    private readonly Validation validation;
    private readonly HashSet<string> read = new(StringComparer.Ordinal);

    internal TomlSection(TomlTable table, string path, Validation validation)
    {
        this.table = table;
        this.path = path;
        this.validation = validation;
    }

    internal int Int(string key, int fallback) =>
        Read(key, fallback, raw => raw switch
        {
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            _ => Reject<int>(key, "a whole number", raw),
        });

    internal double Double(string key, double fallback) =>
        Read(key, fallback, raw => raw switch
        {
            double d => d,
            long l => l,
            _ => Reject<double>(key, "a number", raw),
        });

    internal bool Bool(string key, bool fallback) =>
        Read(key, fallback, raw => raw switch
        {
            bool b => b,
            _ => Reject<bool>(key, "true or false", raw),
        });

    internal Money Money(string key, Money fallback) =>
        Read(key, fallback, raw => raw switch
        {
            double d => Engine.Money.FromEuros((decimal)d),
            long l => Engine.Money.FromEuros(l),
            _ => Reject<Money>(key, "an amount in euros", raw),
        });

    /// <summary>A free string, for a label rather than a choice out of a known set.</summary>
    internal string Text(string key, string fallback) =>
        Read(key, fallback, raw => raw switch
        {
            string s => s,
            _ => Reject<string>(key, "a string", raw),
        });

    internal string Choice(string key, string fallback, IReadOnlyList<string> allowed) =>
        Read(key, fallback, raw => raw switch
        {
            string s when allowed.Contains(s, StringComparer.Ordinal) => s,
            _ => Reject<string>(key, "one of " + string.Join(", ", allowed), raw),
        });

    /// <summary>Whether the file actually said something for this key, rather than defaulting.</summary>
    internal bool Has(string key) => table.ContainsKey(key);

    internal TomlTable? Subtable(string key)
    {
        read.Add(key);

        if (!table.TryGetValue(key, out var raw))
        {
            return null;
        }

        if (raw is TomlTable subtable)
        {
            return subtable;
        }

        validation.Fail($"{path}.{key}", "a table", Describe(raw));
        return null;
    }

    /// <summary>
    /// Every subtable of this section, in **name order**, ignoring keys already read as scalars.
    ///
    /// Name order rather than file order, because for `[archetypes]` the order of the types decides
    /// which household is assigned to which. Two files stating the same types in a different order
    /// have to be the same population, or a scenario and its own control could diverge over nothing
    /// but the order somebody typed the sections in.
    ///
    /// Skipping keys already read is what lets a section hold both scalars and subtables —
    /// `[archetypes]` has a `sigma_idio` and a table per type. It means this must be called
    /// **after** the section's own scalars, which is the natural order anyway.
    /// </summary>
    internal IReadOnlyList<string> Subtables()
    {
        var names = new List<string>();

        foreach (var entry in table)
        {
            if (read.Contains(entry.Key))
            {
                continue;
            }

            if (entry.Value is TomlTable)
            {
                names.Add(entry.Key);
            }
            else
            {
                read.Add(entry.Key);
                validation.Fail($"{path}.{entry.Key}", "a table", Describe(entry.Value));
            }
        }

        names.Sort(StringComparer.Ordinal);

        return names;
    }

    /// <summary>
    /// Everything in this table nobody asked for. Called after the last read, so the set of known
    /// keys is whatever the loader actually consumed rather than a second list to keep in step.
    /// </summary>
    internal void RejectUnknownKeys()
    {
        foreach (var key in table.Keys.Where(k => !read.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            validation.Fail(
                $"{path}.{key}",
                "a parameter in spec/02-PARAMETERS.md — a misspelled key would otherwise run at "
                + "the default and look like the run that was asked for",
                "an unknown key");
        }
    }

    private T Read<T>(string key, T fallback, Func<object, T> convert)
    {
        read.Add(key);

        return table.TryGetValue(key, out var raw) ? convert(raw) : fallback;
    }

    private T Reject<T>(string key, string expected, object raw)
    {
        validation.Fail($"{path}.{key}", expected, Describe(raw));
        return default!;
    }

    private static string Describe(object? raw) => raw switch
    {
        null => "nothing",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => raw.GetType().Name,
    };
}
