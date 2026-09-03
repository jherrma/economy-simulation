using EconomySimulation.Engine.Configuration;

namespace EconomySimulation.Gates;

/// <summary>
/// The two scenarios the gates need, as `spec/01-SIMULATION.md` §9 defines them.
///
/// The campaign's own scenario definitions belong to 09-01 and are not written yet. When they
/// arrive these should be replaced by them rather than kept beside them: two definitions of
/// `credit_high` that drift apart would make a green gate say nothing about the runs that produce
/// the results.
/// </summary>
public static class Scenarios
{
    /// <summary>The baseline. It is the default configuration, because every switch defaults to off.</summary>
    public static SimulationParameters CreditOff(SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters with { Run = parameters.Run with { Scenario = "credit_off" } };
    }

    /// <summary>`θ ~ U(0.4, 0.9)`, credit on. The treatment arm of the experiment.</summary>
    public static SimulationParameters CreditHigh(SimulationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return parameters with
        {
            Run = parameters.Run with { Scenario = "credit_high" },
            Credit = parameters.Credit with { CreditEnabled = true, ThetaMin = 0.4, ThetaMax = 0.9 },
        };
    }
}
