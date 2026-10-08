using Gorm.Core.Primitives;

namespace Gorm.Application.Mutations.Edges;

/// <summary>
/// Represents an edge mutation item.
/// </summary>
public sealed class GraphMutationEdge
{
    /// <summary>
    /// Gets or sets the edge.
    /// </summary>
    public required Edge Edge { get; init; }

    /// <summary>
    /// Gets or sets the source node.
    /// </summary>
    public required Node From { get; init; }

    /// <summary>
    /// Gets or sets the target node.
    /// </summary>
    public required Node To { get; init; }

    /// <summary>
    /// Gets or sets the requested operation.
    /// </summary>
    public required GraphMutationEdgeOperation Operation { get; init; }

    /// <summary>
    /// Gets or sets the optional business key.
    /// </summary>
    public string? Key { get; init; }
}