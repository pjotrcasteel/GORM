namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Describes why a projection cache operation was rejected.
/// </summary>
public enum GraphProjectionCacheFailureReason
{
    /// <summary>
    /// A load returned no snapshot.
    /// </summary>
    MissingSnapshot,

    /// <summary>
    /// A load returned a snapshot for another explicit projection key.
    /// </summary>
    ProjectionKeyMismatch,

    /// <summary>
    /// A load returned a source version below the caller's minimum version.
    /// </summary>
    StaleSnapshot,

    /// <summary>
    /// A concurrent cache update or invalidation made an in-progress operation unsafe to publish.
    /// </summary>
    ConcurrentModification,

    /// <summary>
    /// Repeated concurrent changes prevented a stable cache result.
    /// </summary>
    RefreshLimitExceeded
}