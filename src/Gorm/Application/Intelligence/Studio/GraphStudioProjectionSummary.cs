namespace Gorm.Application.Intelligence.Studio;

/// <summary>
/// Serializable summary of the exact projection shown or analysed by GormStudio.
/// </summary>
public sealed class GraphStudioProjectionSummary
{
    /// <summary>
    /// Gets the logical projection key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the monotonic source version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the direct parent version, when known.
    /// </summary>
    public long? ParentVersion { get; init; }

    /// <summary>
    /// Gets the snapshot creation instant.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the snapshot origin.
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    /// Gets the deterministic topology fingerprint.
    /// </summary>
    public required string TopologyFingerprint { get; init; }

    /// <summary>
    /// Gets projected node count.
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// Gets projected edge count.
    /// </summary>
    public required int EdgeCount { get; init; }

    /// <summary>
    /// Gets projected isolated node count.
    /// </summary>
    public required int IsolatedNodeCount { get; init; }

    /// <summary>
    /// Gets directed graph density.
    /// </summary>
    public required double Density { get; init; }
}