using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Represents a node connected to one or more other communities.
/// </summary>
public sealed class GraphCommunityBridgeNode
{
    /// <summary>
    /// Gets the projected node.
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// Gets the node's final community identifier.
    /// </summary>
    public required int CommunityId { get; init; }

    /// <summary>
    /// Gets the number of selected incident boundary edges.
    /// </summary>
    public required int ExternalEdgeCount { get; init; }

    /// <summary>
    /// Gets the number of distinct neighbouring communities.
    /// </summary>
    public required int NeighboringCommunityCount { get; init; }

    /// <summary>
    /// Gets total selected incident boundary weight.
    /// </summary>
    public required double ExternalWeight { get; init; }

    /// <summary>
    /// Gets external weight divided by all selected incident weight.
    /// </summary>
    public required double ExternalWeightRatio { get; init; }
}