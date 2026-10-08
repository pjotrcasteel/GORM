namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Bounds deterministic scenario replay.
/// </summary>
public sealed class GraphScenarioReplayOptions
{
    /// <summary>
    /// Gets or sets the maximum number of events in one stream or replay.
    /// </summary>
    public int MaximumEvents { get; init; } = 100_000;
}