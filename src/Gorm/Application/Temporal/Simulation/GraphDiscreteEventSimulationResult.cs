using System.Collections.ObjectModel;
using Gorm.Application.Temporal.Scenarios;

namespace Gorm.Application.Temporal.Simulation;

/// <summary>
/// Contains the final scenario and full deterministic event execution trace.
/// </summary>
public sealed class GraphDiscreteEventSimulationResult
{
    internal GraphDiscreteEventSimulationResult(GraphScenario scenario, GraphSimulationStep[] steps, int pendingEventCount, DateTimeOffset startedAt, TimeSpan maximumDuration)
    {
        Scenario = scenario;
        Steps = Array.AsReadOnly(steps);
        PendingEventCount = pendingEventCount;
        StartedAt = startedAt;
        MaximumDuration = maximumDuration;
        Truncated = pendingEventCount > 0;
        Explanation =
            $"Processed {steps.Length} deterministic events for scenario '{scenario.Id.Value}'; " +
            $"{pendingEventCount} events remained beyond the {maximumDuration} horizon.";
    }

    /// <summary>
    /// Gets the final immutable scenario revision.
    /// </summary>
    public GraphScenario Scenario { get; }

    /// <summary>
    /// Gets processed steps in deterministic clock/priority/id order.
    /// </summary>
    public ReadOnlyCollection<GraphSimulationStep> Steps { get; }

    /// <summary>
    /// Gets events beyond the configured simulation horizon.
    /// </summary>
    public int PendingEventCount { get; }

    /// <summary>
    /// Gets whether the time horizon excluded scheduled events.
    /// </summary>
    public bool Truncated { get; }

    /// <summary>
    /// Gets the normalized UTC simulation start.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the configured simulation horizon.
    /// </summary>
    public TimeSpan MaximumDuration { get; }

    /// <summary>
    /// Gets a reproducible execution explanation.
    /// </summary>
    public string Explanation { get; }
}