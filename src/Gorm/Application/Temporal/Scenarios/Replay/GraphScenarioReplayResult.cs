namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Contains the deterministic result and evidence of replaying one scenario event stream.
/// </summary>
public sealed class GraphScenarioReplayResult
{
    /// <summary>
    /// Gets the fully replayed immutable scenario.
    /// </summary>
    public required GraphScenario Scenario { get; init; }

    /// <summary>
    /// Gets the number of applied events.
    /// </summary>
    public required int AppliedEventCount { get; init; }

    /// <summary>
    /// Gets the final event identifier, or a deterministic empty-stream identity.
    /// </summary>
    public required string StreamFingerprint { get; init; }

    /// <summary>
    /// Gets a reproducible replay explanation.
    /// </summary>
    public required string Explanation { get; init; }
}