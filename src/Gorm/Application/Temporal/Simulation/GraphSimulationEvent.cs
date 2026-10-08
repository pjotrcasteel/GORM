using Gorm.Application.Temporal.Scenarios;

namespace Gorm.Application.Temporal.Simulation;

/// <summary>
/// Schedules one detached scenario mutation on the deterministic simulation clock.
/// </summary>
public sealed class GraphSimulationEvent
{
    /// <summary>
    /// Gets the caller-defined stable event identifier.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Gets the non-negative offset from simulation start.
    /// </summary>
    public required TimeSpan Offset { get; init; }

    /// <summary>
    /// Gets deterministic ordering priority; lower values run first at the same offset.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets the detached scenario mutation executed at this event.
    /// </summary>
    public required GraphScenarioMutation Mutation { get; init; }
}