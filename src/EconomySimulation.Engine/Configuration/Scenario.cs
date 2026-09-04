using FluentResults;
using Tomlyn;
using Tomlyn.Model;

namespace EconomySimulation.Engine.Configuration;

/// <summary>
/// A named override of the defaults: one small file, so the difference between two runs is visible
/// on one screen.
///
/// A scenario file is an ordinary partial configuration, loaded by the ordinary loader, which is
/// what gives it 02-02's absent-versus-unknown asymmetry for free — an absent key defaults, and a
/// misspelled key fails rather than silently running the baseline. The name comes from the
/// **filename**, never from inside the file, so the two cannot disagree.
/// </summary>
public sealed class Scenario
{
    private readonly string toml;

    private Scenario(string name, string toml, IReadOnlyList<string> overrides, SimulationParameters parameters)
    {
        Name = name;
        this.toml = toml;
        Overrides = overrides;
        Parameters = parameters;
    }

    /// <summary>
    /// The five of `01-SIMULATION.md` §9, in the order the write-up reports them, followed by the
    /// typed sweep of `02-PARAMETERS.md` §3.5.
    ///
    /// The sweep is four tables times two arms, and the arms come in pairs deliberately: a typed
    /// table is a **different baseline economy** — its population-mean `kappa` is below 1 in every
    /// category — so its treatment arm has to be compared against a control carrying the same
    /// table. Comparing a typed `credit_high` against the untyped `credit_off` would measure the
    /// table and the credit together and attribute both to credit.
    ///
    /// The fifth table of the grid is the identity, and it is not listed here because it is
    /// `credit_off` and `credit_high` above: the default configuration *is* the identity table.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "credit_off",
        "credit_low",
        "credit_high",
        "credit_high_no_money_creation",
        "credit_high_willingness_rationing",
        "typed_credit_off",
        "typed_credit_high",
        "typed_w_only_credit_off",
        "typed_w_only_credit_high",
        "typed_kappa_only_credit_off",
        "typed_kappa_only_credit_high",
        "typed_kappa_neutral_credit_off",
        "typed_kappa_neutral_credit_high",
    ];

    /// <summary>
    /// The five tables of §3.5's sweep grid, each with the scenarios that run it — the control arm
    /// first.
    ///
    /// Named here rather than inferred from the filenames, because the grid is a claim about what
    /// the sweep covers and a claim that reads itself off a directory listing is not a claim.
    /// </summary>
    public static IReadOnlyList<(string Table, IReadOnlyList<string> Scenarios)> SweepGrid { get; } =
    [
        ("identity", new[] { "credit_off", "credit_high" }),
        ("typed", new[] { "typed_credit_off", "typed_credit_high" }),
        ("typed_w_only", new[] { "typed_w_only_credit_off", "typed_w_only_credit_high" }),
        ("typed_kappa_only", new[] { "typed_kappa_only_credit_off", "typed_kappa_only_credit_high" }),
        ("typed_kappa_neutral", new[] { "typed_kappa_neutral_credit_off", "typed_kappa_neutral_credit_high" }),
    ];

    /// <summary>The scenario id, from the filename. Written on every output row.</summary>
    public string Name { get; }

    /// <summary>
    /// The keys the file itself sets, fully qualified — what the scenario **claims** to change.
    ///
    /// Not the same thing as what it changes: a key may be pinned to a value it already has, which
    /// is deliberate (`credit_low` pins a theta that currently equals the default so that it will
    /// not follow the default if the default moves). What must never happen is the other way round
    /// — a change nobody declared — and that is what comparing this against <see cref="Changes"/>
    /// catches.
    /// </summary>
    public IReadOnlyList<string> Overrides { get; }

    /// <summary>This scenario applied to the defaults — the parameters the campaign runs.</summary>
    public SimulationParameters Parameters { get; }

    /// <summary>
    /// This scenario applied to something other than the defaults.
    ///
    /// For a caller that has already changed the parameters for its own reasons — a gate that
    /// scaled every price, a probe sweeping the loan rate — and needs the scenario laid on top
    /// without losing what it did. Composing the other way round would work too, but only by
    /// accident: it relies on the caller's change and the scenario's never touching the same key,
    /// which nobody would notice going wrong.
    /// </summary>
    public Result<SimulationParameters> ApplyTo(SimulationParameters basis)
    {
        ArgumentNullException.ThrowIfNull(basis);

        var applied = ConfigurationLoader.FromToml(toml, basis with { Run = basis.Run with { Scenario = Name } });

        return applied.IsFailed
            ? applied
            : Result.Ok(applied.Value with { Run = applied.Value.Run with { Scenario = Name } });
    }

    /// <summary>
    /// The keys whose effective value differs from the defaults, other than `run.scenario`, which
    /// is set from the filename and therefore always differs.
    /// </summary>
    public IReadOnlyList<string> Changes =>
        [.. Parameters.DifferencesFrom(SimulationParameters.Default).Where(k => k != ScenarioKey)];

    /// <summary>All five, in <see cref="Names"/> order, from a directory of scenario files.</summary>
    public static Result<IReadOnlyList<Scenario>> All(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var loaded = new List<Scenario>(Names.Count);
        var failures = new List<IError>();

        // The named list rather than whatever the directory holds. A campaign that silently becomes
        // twelve scenarios because a file was renamed is a campaign whose missing arm nobody
        // notices.
        foreach (var name in Names)
        {
            var scenario = FromFile(Path.Combine(directory, name + ".toml"));

            if (scenario.IsFailed)
            {
                failures.AddRange(scenario.Errors);
            }
            else
            {
                loaded.Add(scenario.Value);
            }
        }

        return failures.Count > 0
            ? Result.Fail<IReadOnlyList<Scenario>>(failures)
            : Result.Ok<IReadOnlyList<Scenario>>(loaded);
    }

    public static Result<Scenario> FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result.Fail<Scenario>($"scenario file: expected a readable path, got {path}");
        }

        return FromToml(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path));
    }

    public static Result<Scenario> FromToml(string name, string toml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(toml);

        var loaded = ConfigurationLoader.FromToml(toml);

        if (loaded.IsFailed)
        {
            return Result.Fail<Scenario>(loaded.Errors);
        }

        var overrides = new List<string>();
        Flatten(TomlSerializer.Deserialize<TomlTable>(toml) ?? [], string.Empty, overrides);
        overrides.Sort(StringComparer.Ordinal);

        if (overrides.Contains(ScenarioKey, StringComparer.Ordinal))
        {
            return Result.Fail<Scenario>(
                $"scenario '{name}': expected the id to come from the filename, got a file that "
                + $"sets {ScenarioKey} — two places to write the name is one place for them to "
                + "disagree, and the output rows would then be labelled with the loser");
        }

        var parameters = loaded.Value with { Run = loaded.Value.Run with { Scenario = name } };

        return Result.Ok(new Scenario(name, toml, overrides, parameters));
    }

    private const string ScenarioKey = "run.scenario";

    private static void Flatten(TomlTable table, string prefix, List<string> into)
    {
        foreach (var entry in table)
        {
            var key = prefix.Length == 0 ? entry.Key : prefix + "." + entry.Key;

            if (entry.Value is TomlTable nested)
            {
                Flatten(nested, key, into);
            }
            else
            {
                into.Add(key);
            }
        }
    }
}
