namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Captures replay-verifiable identity for one immutable scenario revision.
/// </summary>
public sealed class GraphScenarioCheckpoint
{
    /// <summary>
    /// Gets the scenario identifier.
    /// </summary>
    public required GraphScenarioId ScenarioId { get; init; }

    /// <summary>
    /// Gets the expected baseline snapshot id.
    /// </summary>
    public required string BaselineSnapshotId { get; init; }

    /// <summary>
    /// Gets the expected event-stream fingerprint.
    /// </summary>
    public required string StreamFingerprint { get; init; }

    /// <summary>
    /// Gets the expected final snapshot id.
    /// </summary>
    public required string FinalSnapshotId { get; init; }

    /// <summary>
    /// Gets the expected scenario revision.
    /// </summary>
    public required int Revision { get; init; }

    /// <summary>
    /// Creates an integrity checkpoint from a scenario and its deterministic event stream.
    /// </summary>
    /// <param name="scenario">Scenario revision.</param>
    /// <param name="eventStream">Event stream representing every scenario mutation.</param>
    /// <returns>The replay-verifiable checkpoint.</returns>
    public static GraphScenarioCheckpoint Create(GraphScenario scenario, GraphScenarioEventStream eventStream)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(eventStream);
        if (eventStream.ScenarioId != scenario.Id || eventStream.Events.Count != scenario.Revision)
        {
            throw new ArgumentException("The event stream does not represent the supplied scenario revision.", nameof(eventStream));
        }

        return new GraphScenarioCheckpoint
        {
            ScenarioId = scenario.Id,
            BaselineSnapshotId = scenario.Baseline.Identity.SnapshotId,
            StreamFingerprint = eventStream.StreamFingerprint,
            FinalSnapshotId = scenario.Current.Identity.SnapshotId,
            Revision = scenario.Revision
        };
    }
}