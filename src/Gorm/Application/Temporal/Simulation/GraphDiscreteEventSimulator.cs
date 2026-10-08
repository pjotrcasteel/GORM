using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Simulation;

/// <summary>
/// Executes deterministic clock-ordered scenario mutations without provider or database interaction.
/// </summary>
public static class GraphDiscreteEventSimulator
{
    /// <summary>
    /// Runs one bounded discrete-event simulation.
    /// </summary>
    /// <param name="baseline">Immutable initial world.</param>
    /// <param name="scenarioId">Isolated simulation scenario identifier.</param>
    /// <param name="startedAt">Absolute UTC clock origin.</param>
    /// <param name="events">Scheduled events in any input order.</param>
    /// <param name="options">Event, horizon and scenario safety options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final scenario and deterministic execution trace.</returns>
    public static GraphDiscreteEventSimulationResult Run(
        GraphWorldSnapshot baseline,
        GraphScenarioId scenarioId,
        DateTimeOffset startedAt,
        IEnumerable<GraphSimulationEvent> events,
        GraphDiscreteEventSimulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(scenarioId);
        ArgumentNullException.ThrowIfNull(events);
        options ??= new GraphDiscreteEventSimulationOptions();
        ValidateOptions(options);
        var scheduled = MaterializeEvents(events, options.MaximumEvents, cancellationToken);
        var normalizedStart = startedAt.ToUniversalTime();
        var processable = scheduled
            .Where(item => item.Offset <= options.MaximumDuration)
            .OrderBy(item => item.Offset)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .ToArray();
        var scenario = GraphScenario.Fork(baseline, scenarioId, options.ScenarioOptions);
        var steps = new GraphSimulationStep[processable.Length];

        for (var index = 0; index < processable.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var simulationEvent = processable[index];
            var simulatedAt = normalizedStart + simulationEvent.Offset;
            var applied = scenario.Apply(simulationEvent.Mutation.At(simulatedAt, simulatedAt), cancellationToken);
            scenario = applied.Scenario;
            steps[index] = new GraphSimulationStep
            {
                Sequence = index + 1,
                EventId = simulationEvent.EventId,
                SimulatedAt = simulatedAt,
                Difference = applied.Difference,
                SnapshotId = scenario.Current.Identity.SnapshotId,
                CascadedEdgeRemovals = applied.CascadedEdgeRemovals
            };
        }

        return new GraphDiscreteEventSimulationResult(scenario, steps, scheduled.Count - processable.Length, normalizedStart, options.MaximumDuration);
    }

    private static List<GraphSimulationEvent> MaterializeEvents(IEnumerable<GraphSimulationEvent> events, int maximumEvents, CancellationToken cancellationToken)
    {
        var result = new List<GraphSimulationEvent>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var simulationEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(simulationEvent);
            if (result.Count == maximumEvents)
            {
                throw new InvalidOperationException($"Simulation exceeded MaximumEvents ({maximumEvents}).");
            }

            if (string.IsNullOrWhiteSpace(simulationEvent.EventId) || !identifiers.Add(simulationEvent.EventId))
            {
                throw new ArgumentException("Simulation event ids must be non-empty and unique.", nameof(events));
            }

            if (simulationEvent.Offset < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(events), "Simulation event offsets cannot be negative.");
            }

            ArgumentNullException.ThrowIfNull(simulationEvent.Mutation);
            result.Add(simulationEvent);
        }

        return result;
    }

    private static void ValidateOptions(GraphDiscreteEventSimulationOptions options)
    {
        if (options.MaximumEvents <= 0 || options.MaximumDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Simulation limits must be non-negative and MaximumEvents positive.");
        }

        ArgumentNullException.ThrowIfNull(options.ScenarioOptions);
    }
}