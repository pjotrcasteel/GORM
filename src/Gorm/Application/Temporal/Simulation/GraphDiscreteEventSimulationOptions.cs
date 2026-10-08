using Gorm.Application.Temporal.Scenarios;

namespace Gorm.Application.Temporal.Simulation;

/// <summary>
/// Bounds one deterministic discrete-event simulation.
/// </summary>
public sealed class GraphDiscreteEventSimulationOptions
{
    /// <summary>
    /// Gets or sets the maximum number of scheduled source events.
    /// </summary>
    public int MaximumEvents { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets the inclusive simulation horizon.
    /// </summary>
    public TimeSpan MaximumDuration { get; init; } = TimeSpan.FromDays(3650);

    /// <summary>
    /// Gets or sets scenario growth limits used by the simulated branch.
    /// </summary>
    public GraphScenarioOptions ScenarioOptions { get; init; } = new();
}