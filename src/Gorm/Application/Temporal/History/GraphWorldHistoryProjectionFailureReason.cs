namespace Gorm.Application.Temporal.History;

/// <summary>
/// Classifies failures while resolving a temporal world from GORM history.
/// </summary>
public enum GraphWorldHistoryProjectionFailureReason
{
    /// <summary>
    /// The configured maximum number of history entries was exceeded.
    /// </summary>
    EntryLimitExceeded,

    /// <summary>
    /// Multiple equally recent records make the selected state ambiguous.
    /// </summary>
    AmbiguousState,

    /// <summary>
    /// A history envelope does not match its captured GORM entity.
    /// </summary>
    InvalidEnvelope
}