using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Snapshots;

/// <summary>
/// Represents one detached temporal graph world. Captured state cannot be changed through materialized entities.
/// </summary>
public sealed class GraphWorldSnapshot
{
    private readonly GraphWorldNodeState[] _nodes;
    private readonly GraphWorldEdgeState[] _edges;

    private GraphWorldSnapshot(GraphWorldSnapshotIdentity identity, GraphWorldNodeState[] nodes, GraphWorldEdgeState[] edges)
    {
        Identity = identity;
        _nodes = nodes;
        _edges = edges;
        Nodes = Array.AsReadOnly(nodes);
        Edges = Array.AsReadOnly(edges);
    }

    /// <summary>
    /// Gets the temporal and content identity of this world state.
    /// </summary>
    public GraphWorldSnapshotIdentity Identity { get; }

    /// <summary>
    /// Gets immutable detached node states ordered by identifier.
    /// </summary>
    public ReadOnlyCollection<GraphWorldNodeState> Nodes { get; }

    /// <summary>
    /// Gets immutable detached edge states ordered by identifier.
    /// </summary>
    public ReadOnlyCollection<GraphWorldEdgeState> Edges { get; }

    /// <summary>
    /// Captures materialized GORM entities into an immutable temporal world snapshot.
    /// </summary>
    /// <param name="worldKey">Stable source and scope key.</param>
    /// <param name="version">Monotonic source version.</param>
    /// <param name="validAt">Business-valid instant represented by this state.</param>
    /// <param name="recordedAt">Instant at which this state became known.</param>
    /// <param name="nodes">Materialized GORM nodes.</param>
    /// <param name="edges">Materialized GORM edges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A detached immutable world snapshot.</returns>
    public static GraphWorldSnapshot Capture(CaptureParameters inputs)
    {
        var worldKey = inputs.WorldKey;
        var version = inputs.Version;
        var validAt = inputs.ValidAt;
        var recordedAt = inputs.RecordedAt;
        var nodes = inputs.Nodes;
        var edges = inputs.Edges;
        var cancellationToken = inputs.CancellationToken;

        ArgumentNullException.ThrowIfNull(worldKey);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputs), version, "The supplied world version cannot be negative.");
        }

        var nodeStates = CaptureNodes(nodes, cancellationToken);
        var edgeStates = CaptureEdges(edges, cancellationToken);

        _ = GraphProjection.Create(nodeStates.Select(state => state.Materialize()), edgeStates.Select(state => state.Materialize()));

        var normalizedValidAt = validAt.ToUniversalTime();
        var normalizedRecordedAt = recordedAt.ToUniversalTime();
        var contentFingerprint = CalculateContentFingerprint(nodeStates, edgeStates);
        var snapshotId = CalculateSnapshotId(worldKey, version, normalizedValidAt, normalizedRecordedAt, contentFingerprint);

        return new GraphWorldSnapshot(
            new GraphWorldSnapshotIdentity(worldKey, version, normalizedValidAt, normalizedRecordedAt, contentFingerprint, snapshotId),
            nodeStates,
            edgeStates);
    }

    /// <summary>
    /// Captures an existing versioned Intelligence projection as a detached temporal world.
    /// </summary>
    /// <param name="source">Versioned provider-neutral projection to capture.</param>
    /// <param name="validAt">Business-valid instant represented by this state.</param>
    /// <param name="recordedAt">
    /// Optional recorded instant. The source snapshot creation time is used when omitted.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A detached immutable world snapshot.</returns>
    public static GraphWorldSnapshot Capture(
        GraphProjectionSnapshot source,
        DateTimeOffset validAt,
        DateTimeOffset? recordedAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return Capture(
            new CaptureParameters
            {
                WorldKey = source.Key,
                Version = source.Version,
                ValidAt = validAt,
                RecordedAt = recordedAt ?? source.Metadata.CreatedAt,
                Nodes = source.Projection.Nodes,
                Edges = source.Projection.Edges,
                CancellationToken = cancellationToken
            });
    }

    /// <summary>
    /// Materializes a fresh GORM Intelligence projection without exposing the snapshot's captured state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new graph projection.</returns>
    public GraphProjection CreateProjection(CancellationToken cancellationToken = default)
    {
        var nodes = new Node[_nodes.Length];
        for (var index = 0; index < nodes.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes[index] = _nodes[index].Materialize();
        }

        var edges = new Edge[_edges.Length];
        for (var index = 0; index < edges.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            edges[index] = _edges[index].Materialize();
        }

        return GraphProjection.Create(nodes, edges);
    }

    /// <summary>
    /// Materializes a fresh versioned Intelligence snapshot with temporal provenance attached.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new versioned Intelligence snapshot.</returns>
    public GraphProjectionSnapshot CreateIntelligenceSnapshot(CancellationToken cancellationToken = default)
    {
        var projection = CreateProjection(cancellationToken);
        var metadata = GraphProjectionSnapshotMetadata.Create(Identity.WorldKey, Identity.Version, projection, Identity.RecordedAt, "temporal-world");

        return new GraphProjectionSnapshot(Identity.WorldKey, Identity.Version, projection, metadata);
    }

    private static GraphWorldNodeState[] CaptureNodes(IEnumerable<Node> nodes, CancellationToken cancellationToken)
    {
        var result = new List<GraphWorldNodeState>();
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(node);
            result.Add(new GraphWorldNodeState(node));
        }

        return [.. result.OrderBy(state => state.Id)];
    }

    private static GraphWorldEdgeState[] CaptureEdges(IEnumerable<Edge> edges, CancellationToken cancellationToken)
    {
        var result = new List<GraphWorldEdgeState>();
        foreach (var edge in edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(edge);
            result.Add(new GraphWorldEdgeState(edge));
        }

        return [.. result.OrderBy(state => state.Id)];
    }

    private static string CalculateContentFingerprint(IEnumerable<GraphWorldNodeState> nodes, IEnumerable<GraphWorldEdgeState> edges)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> identifier = stackalloc byte[16];

        foreach (var node in nodes)
        {
            hash.AppendData([0x4E]);
            node.Id.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            AppendType(hash, node.EntityType);
            hash.AppendData(node.Payload);
        }

        foreach (var edge in edges)
        {
            hash.AppendData([0x45]);
            edge.Id.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            edge.FromId.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            edge.ToId.TryWriteBytes(identifier);
            hash.AppendData(identifier);
            AppendType(hash, edge.EntityType);
            hash.AppendData(edge.Payload);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string CalculateSnapshotId(GraphProjectionKey worldKey, long version, DateTimeOffset validAt, DateTimeOffset recordedAt, string contentFingerprint)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> number = stackalloc byte[sizeof(long)];
        hash.AppendData(Encoding.UTF8.GetBytes(worldKey.Value));
        hash.AppendData([0]);
        BinaryPrimitives.WriteInt64LittleEndian(number, version);
        hash.AppendData(number);
        BinaryPrimitives.WriteInt64LittleEndian(number, validAt.UtcTicks);
        hash.AppendData(number);
        BinaryPrimitives.WriteInt64LittleEndian(number, recordedAt.UtcTicks);
        hash.AppendData(number);
        hash.AppendData(Convert.FromHexString(contentFingerprint));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendType(IncrementalHash hash, Type type)
    {
        var typeName = type.FullName ?? type.Name;
        hash.AppendData(Encoding.UTF8.GetBytes(typeName));
        hash.AppendData([0]);
    }

    /// <summary>
    /// Groups the inputs for Capture.
    /// </summary>
    public sealed class CaptureParameters
    {
        /// <summary>
        /// Gets or initializes worldKey.
        /// </summary>
        public required GraphProjectionKey WorldKey { get; init; }

        /// <summary>
        /// Gets or initializes version.
        /// </summary>
        public required long Version { get; init; }

        /// <summary>
        /// Gets or initializes validAt.
        /// </summary>
        public required DateTimeOffset ValidAt { get; init; }

        /// <summary>
        /// Gets or initializes recordedAt.
        /// </summary>
        public required DateTimeOffset RecordedAt { get; init; }

        /// <summary>
        /// Gets or initializes nodes.
        /// </summary>
        public required IEnumerable<Node> Nodes { get; init; }

        /// <summary>
        /// Gets or initializes edges.
        /// </summary>
        public required IEnumerable<Edge> Edges { get; init; }

        /// <summary>
        /// Gets or initializes cancellationToken.
        /// </summary>
        public CancellationToken CancellationToken { get; init; } = default;
    }
}