using System.Security.Cryptography;
using System.Text;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Carries immutable provenance and identity metadata for a projection snapshot.
/// </summary>
public sealed class GraphProjectionSnapshotMetadata
{
    private GraphProjectionSnapshotMetadata()
    {
    }

    /// <summary>
    /// Gets the logical projection key.
    /// </summary>
    public required GraphProjectionKey Key { get; init; }

    /// <summary>
    /// Gets the monotonic source version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets the direct parent version for an incremental snapshot, when known.
    /// </summary>
    public long? ParentVersion { get; init; }

    /// <summary>
    /// Gets the UTC instant at which the immutable snapshot was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the application-readable origin, for example materialized, delta or rebuild.
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    /// Gets the projected node count.
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// Gets the projected edge count.
    /// </summary>
    public required int EdgeCount { get; init; }

    /// <summary>
    /// Gets a deterministic SHA-256 identity of node/edge identifiers, types and endpoints.
    /// </summary>
    public required string TopologyFingerprint { get; init; }

    internal GraphProjection ProjectionIdentity { get; init; } = null!;

    /// <summary>
    /// Creates validated metadata for a projection.
    /// </summary>
    public static GraphProjectionSnapshotMetadata Create(
        GraphProjectionKey key,
        long version,
        GraphProjection projection,
        DateTimeOffset? createdAt = null,
        string origin = "materialized",
        long? parentVersion = null) =>
        Create(key, version, projection, new GraphProjectionSnapshotMetadataCreationOptions { CreatedAt = createdAt, Origin = origin, ParentVersion = parentVersion });

    /// <summary>
    /// Creates validated metadata for a projection with explicit creation options.
    /// </summary>
    public static GraphProjectionSnapshotMetadata Create(GraphProjectionKey key, long version, GraphProjection projection, GraphProjectionSnapshotMetadataCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(version);

        if (options.ParentVersion is < 0 || options.ParentVersion >= version)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.ParentVersion, "A parent version must be non-negative and older than the snapshot version.");
        }

        if (string.IsNullOrWhiteSpace(options.Origin))
        {
            throw new ArgumentException("A snapshot origin cannot be empty.", nameof(options));
        }

        return new GraphProjectionSnapshotMetadata
        {
            Key = key,
            Version = version,
            ParentVersion = options.ParentVersion,
            CreatedAt = options.CreatedAt ?? options.TimeProvider.GetUtcNow(),
            Origin = options.Origin,
            NodeCount = projection.Nodes.Count,
            EdgeCount = projection.Edges.Count,
            TopologyFingerprint = CalculateTopologyFingerprint(projection),
            ProjectionIdentity = projection
        };
    }

    private static string CalculateTopologyFingerprint(GraphProjection projection)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> identifier = stackalloc byte[16];

        foreach (var node in projection.Nodes.OrderBy(node => node.Id))
        {
            hash.AppendData([0x4E]);
            node.Id.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            AppendType(hash, node.GetType());
        }

        foreach (var edge in projection.Edges.OrderBy(edge => edge.Id))
        {
            hash.AppendData([0x45]);
            edge.Id.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            edge.FromId.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            edge.ToId.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            AppendType(hash, edge.GetType());
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendType(IncrementalHash hash, Type type)
    {
        var typeName = type.FullName ?? type.Name;
        hash.AppendData(Encoding.UTF8.GetBytes(typeName));
        hash.AppendData([0]);
    }
}