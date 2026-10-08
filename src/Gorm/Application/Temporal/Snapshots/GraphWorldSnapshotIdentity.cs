using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Temporal.Snapshots;

/// <summary>
/// Identifies one immutable temporal world state by source, version, valid time, recorded time and content.
/// </summary>
public sealed class GraphWorldSnapshotIdentity
{
    internal GraphWorldSnapshotIdentity(GraphProjectionKey worldKey, long version, DateTimeOffset validAt, DateTimeOffset recordedAt, string contentFingerprint, string snapshotId)
    {
        WorldKey = worldKey;
        Version = version;
        ValidAt = validAt;
        RecordedAt = recordedAt;
        ContentFingerprint = contentFingerprint;
        SnapshotId = snapshotId;
    }

    /// <summary>
    /// Gets the stable application-defined world key.
    /// </summary>
    public GraphProjectionKey WorldKey { get; }

    /// <summary>
    /// Gets the monotonic source version.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the UTC business-valid instant represented by the snapshot.
    /// </summary>
    public DateTimeOffset ValidAt { get; }

    /// <summary>
    /// Gets the UTC instant at which this knowledge was recorded.
    /// </summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>
    /// Gets a deterministic SHA-256 fingerprint of all captured node and edge state.
    /// </summary>
    public string ContentFingerprint { get; }

    /// <summary>
    /// Gets the deterministic SHA-256 identity of the world key, version, times and content.
    /// </summary>
    public string SnapshotId { get; }
}