namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Configures projection snapshot metadata creation.
/// </summary>
public sealed class GraphProjectionSnapshotMetadataCreationOptions
{
    /// <summary>
    /// Gets or initializes the snapshot creation instant.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the application-readable origin.
    /// </summary>
    public string Origin { get; init; } = "materialized";

    /// <summary>
    /// Gets or initializes the direct parent version.
    /// </summary>
    public long? ParentVersion { get; init; }

    /// <summary>
    /// Gets or initializes the time provider used when no creation instant is supplied.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}