namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Represents one content-addressed event in a scenario hash chain.
/// </summary>
public sealed class GraphScenarioEvent
{
    /// <summary>
    /// Gets the contiguous one-based sequence.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Gets the previous event identifier, or null for the first event.
    /// </summary>
    public string? PreviousEventId { get; init; }

    /// <summary>
    /// Gets the deterministic SHA-256 identifier of this event and its predecessor.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Gets the detached scenario mutation.
    /// </summary>
    public required GraphScenarioMutation Mutation { get; init; }
}