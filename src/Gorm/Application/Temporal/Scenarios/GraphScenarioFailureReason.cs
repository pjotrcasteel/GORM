namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Classifies a rejected isolated scenario mutation.
/// </summary>
public enum GraphScenarioFailureReason
{
    /// <summary>
    /// An add targeted an identifier that already exists.
    /// </summary>
    EntityAlreadyExists,

    /// <summary>
    /// An update or remove targeted an identifier that does not exist.
    /// </summary>
    EntityNotFound,

    /// <summary>
    /// The mutation would create an invalid graph topology.
    /// </summary>
    InvalidTopology,

    /// <summary>
    /// The configured maximum number of revisions was reached.
    /// </summary>
    RevisionLimitExceeded,

    /// <summary>
    /// The configured node or edge limit was exceeded.
    /// </summary>
    SizeLimitExceeded,

    /// <summary>
    /// Recorded time moved backwards across scenario revisions.
    /// </summary>
    TemporalOrderViolation
}