using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Represents one detected graph community and its quality metrics.
/// </summary>
public sealed class GraphCommunity
{
    /// <summary>
    /// Gets the deterministic community identifier within its hierarchy level.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets the projected nodes in the community.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }

    /// <summary>
    /// Gets the total weight of selected edges whose endpoints both belong to this community.
    /// </summary>
    public required double InternalWeight { get; init; }

    /// <summary>
    /// Gets the total incident weight crossing the community boundary.
    /// </summary>
    public required double BoundaryWeight { get; init; }

    /// <summary>
    /// Gets boundary weight divided by the smaller graph volume on either side of the boundary.
    /// Lower values indicate a better-separated community.
    /// </summary>
    public required double Conductance { get; init; }

    /// <summary>
    /// Gets the selected internal edge count divided by the possible non-self endpoint pairs.
    /// </summary>
    public required double Density { get; init; }
}