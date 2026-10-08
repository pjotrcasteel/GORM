using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Describes an immutable, ordered set of node and edge changes between two source versions.
/// </summary>
public sealed class GraphProjectionDelta
{
    internal GraphProjectionDelta(GraphProjectionDeltaParameters inputs)
    {
        var key = inputs.Key;
        var baseVersion = inputs.BaseVersion;
        var targetVersion = inputs.TargetVersion;
        var addedNodes = inputs.AddedNodes;
        var updatedNodes = inputs.UpdatedNodes;
        var removedNodeIds = inputs.RemovedNodeIds;
        var addedEdges = inputs.AddedEdges;
        var updatedEdges = inputs.UpdatedEdges;
        var removedEdgeIds = inputs.RemovedEdgeIds;

        Key = key;
        BaseVersion = baseVersion;
        TargetVersion = targetVersion;
        AddedNodes = Array.AsReadOnly(addedNodes);
        UpdatedNodes = Array.AsReadOnly(updatedNodes);
        RemovedNodeIds = [.. removedNodeIds];
        AddedEdges = Array.AsReadOnly(addedEdges);
        UpdatedEdges = Array.AsReadOnly(updatedEdges);
        RemovedEdgeIds = [.. removedEdgeIds];
    }

    /// <summary>
    /// Gets the logical projection key to which this delta belongs.
    /// </summary>
    public GraphProjectionKey Key { get; }

    /// <summary>
    /// Gets the exact source version required before applying the delta.
    /// </summary>
    public long BaseVersion { get; }

    /// <summary>
    /// Gets the source version produced after applying the delta.
    /// </summary>
    public long TargetVersion { get; }

    /// <summary>
    /// Gets nodes added by the delta.
    /// </summary>
    public IReadOnlyList<Node> AddedNodes { get; }

    /// <summary>
    /// Gets replacement values for existing nodes.
    /// </summary>
    public IReadOnlyList<Node> UpdatedNodes { get; }

    /// <summary>
    /// Gets identifiers of removed nodes. Incident edges are removed automatically.
    /// </summary>
    public Guid[] RemovedNodeIds { get; }

    /// <summary>
    /// Gets edges added by the delta.
    /// </summary>
    public IReadOnlyList<Edge> AddedEdges { get; }

    /// <summary>
    /// Gets replacement values for existing edges.
    /// </summary>
    public IReadOnlyList<Edge> UpdatedEdges { get; }

    /// <summary>
    /// Gets identifiers of removed edges.
    /// </summary>
    public Guid[] RemovedEdgeIds { get; }

    /// <summary>
    /// Gets whether the delta contains no entity changes.
    /// </summary>
    public bool IsEmpty =>
        AddedNodes.Count == 0 &&
        UpdatedNodes.Count == 0 &&
        RemovedNodeIds.Length == 0 &&
        AddedEdges.Count == 0 &&
        UpdatedEdges.Count == 0 &&
        RemovedEdgeIds.Length == 0;

    internal sealed class GraphProjectionDeltaParameters
    {
        public required GraphProjectionKey Key { get; init; }

        public required long BaseVersion { get; init; }

        public required long TargetVersion { get; init; }

        public required Node[] AddedNodes { get; init; }

        public required Node[] UpdatedNodes { get; init; }

        public required Guid[] RemovedNodeIds { get; init; }

        public required Edge[] AddedEdges { get; init; }

        public required Edge[] UpdatedEdges { get; init; }

        public required Guid[] RemovedEdgeIds { get; init; }
    }
}