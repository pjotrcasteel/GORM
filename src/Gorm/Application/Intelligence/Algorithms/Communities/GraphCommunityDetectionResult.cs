namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Represents a hierarchical community-detection result.
/// </summary>
public sealed class GraphCommunityDetectionResult
{
    private readonly Dictionary<Guid, int> _communityByNodeId;

    internal GraphCommunityDetectionResult(
        IReadOnlyList<GraphCommunityLevel> levels,
        IReadOnlyList<GraphCommunityBoundaryEdge> boundaryEdges,
        IReadOnlyList<GraphCommunityBridgeNode> bridgeNodes)
    {
        Levels = levels;
        Communities = levels.Count == 0 ? [] : levels[^1].Communities;
        Modularity = levels.Count == 0 ? 0 : levels[^1].Modularity;
        BoundaryEdges = boundaryEdges;
        Anomalies = [.. boundaryEdges.Where(edge => edge.IsAnomalous)];
        BridgeNodes = bridgeNodes;
        _communityByNodeId = Communities
            .SelectMany(community => community.Nodes.Select(node => (NodeId: node.Id, CommunityId: community.Id)))
            .ToDictionary(item => item.NodeId, item => item.CommunityId);
    }

    /// <summary>
    /// Gets all unique hierarchy levels, from the finest detected partition to the final partition.
    /// </summary>
    public IReadOnlyList<GraphCommunityLevel> Levels { get; }

    /// <summary>
    /// Gets communities in the final hierarchy level.
    /// </summary>
    public IReadOnlyList<GraphCommunity> Communities { get; }

    /// <summary>
    /// Gets final directed or undirected modularity.
    /// </summary>
    public double Modularity { get; }

    /// <summary>
    /// Gets selected edges crossing the final partition.
    /// </summary>
    public IReadOnlyList<GraphCommunityBoundaryEdge> BoundaryEdges { get; }

    /// <summary>
    /// Gets boundary edges meeting the configured anomaly threshold.
    /// </summary>
    public IReadOnlyList<GraphCommunityBoundaryEdge> Anomalies { get; }

    /// <summary>
    /// Gets nodes connected to at least one other final community.
    /// </summary>
    public IReadOnlyList<GraphCommunityBridgeNode> BridgeNodes { get; }

    /// <summary>
    /// Gets the final community identifier for a projected node.
    /// </summary>
    public int GetCommunityId(Guid nodeId) =>
        _communityByNodeId.TryGetValue(nodeId, out var communityId)
            ? communityId
            : throw new KeyNotFoundException($"Node '{nodeId}' has no detected community.");
}