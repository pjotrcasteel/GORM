namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Associates a detached graph projection with an explicit logical key and monotonic source version.
/// </summary>
public sealed class GraphProjectionSnapshot
{
    /// <summary>
    /// Initializes a versioned projection snapshot.
    /// </summary>
    public GraphProjectionSnapshot(GraphProjectionKey key, long version, GraphProjection projection)
        : this(key, version, projection, GraphProjectionSnapshotMetadata.Create(key, version, projection))
    {
    }

    /// <summary>
    /// Initializes a versioned projection snapshot with caller-supplied provenance metadata.
    /// </summary>
    public GraphProjectionSnapshot(GraphProjectionKey key, long version, GraphProjection projection, GraphProjectionSnapshotMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(metadata);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "A projection version cannot be negative.");
        }

        if (metadata.Key != key || metadata.Version != version)
        {
            throw new ArgumentException("Snapshot metadata key and version must match the snapshot identity.", nameof(metadata));
        }

        if (!ReferenceEquals(metadata.ProjectionIdentity, projection) ||
            metadata.NodeCount != projection.Nodes.Count ||
            metadata.EdgeCount != projection.Edges.Count)
        {
            throw new ArgumentException("Snapshot metadata must have been created for the supplied projection.", nameof(metadata));
        }

        Key = key;
        Version = version;
        Projection = projection;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the logical source and scope key.
    /// </summary>
    public GraphProjectionKey Key { get; }

    /// <summary>
    /// Gets the monotonic source version represented by this snapshot.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the detached projected graph.
    /// </summary>
    public GraphProjection Projection { get; }

    /// <summary>
    /// Gets immutable source, version, time, shape and topology identity metadata.
    /// </summary>
    public GraphProjectionSnapshotMetadata Metadata { get; }

    /// <summary>
    /// Applies a compatible delta without changing this snapshot.
    /// </summary>
    public GraphProjectionUpdateResult ApplyDelta(GraphProjectionDelta delta, CancellationToken cancellationToken = default) =>
        GraphProjectionUpdater.Apply(this, delta, cancellationToken);

    /// <summary>
    /// Applies a compatible delta or invokes an explicit full-rebuild factory when safe incremental application is impossible.
    /// </summary>
    public Task<GraphProjectionUpdateResult> ApplyDeltaOrRebuildAsync(
        GraphProjectionDelta delta,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        CancellationToken cancellationToken = default) =>
        GraphProjectionUpdater.ApplyOrRebuildAsync(this, delta, rebuildFactory, cancellationToken);
}