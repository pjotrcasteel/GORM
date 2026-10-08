namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Describes why a graph delta could not be applied safely.
/// </summary>
public enum GraphProjectionDeltaFailureReason
{
    /// <summary>
    /// The delta belongs to a different logical projection.
    /// </summary>
    ProjectionKeyMismatch,

    /// <summary>
    /// The current snapshot is not the exact base version required by the delta.
    /// </summary>
    VersionMismatch,

    /// <summary>
    /// An added entity already exists or an updated/removed entity is missing.
    /// </summary>
    EntityStateMismatch,

    /// <summary>
    /// An edge refers to a node that is absent after applying the delta.
    /// </summary>
    InvalidEndpoint,

    /// <summary>
    /// The rebuilt snapshot is stale or belongs to another logical projection.
    /// </summary>
    RebuildRejected
}