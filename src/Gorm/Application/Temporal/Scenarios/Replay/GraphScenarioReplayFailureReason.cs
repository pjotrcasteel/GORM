namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Classifies deterministic scenario event-stream or replay failures.
/// </summary>
public enum GraphScenarioReplayFailureReason
{
    /// <summary>
    /// Event sequences are not contiguous and one-based.
    /// </summary>
    SequenceMismatch,

    /// <summary>
    /// An event identifier or previous-event link does not match deterministic content.
    /// </summary>
    IntegrityMismatch,

    /// <summary>
    /// The configured maximum number of replay events was exceeded.
    /// </summary>
    EventLimitExceeded,

    /// <summary>
    /// Recorded event time moved backwards.
    /// </summary>
    TemporalOrderViolation
}