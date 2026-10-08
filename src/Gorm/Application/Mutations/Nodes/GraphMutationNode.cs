using Gorm.Core.Primitives;

namespace Gorm.Application.Mutations.Nodes;

/// <summary>
/// Represents a node mutation item.
/// </summary>
public sealed class GraphMutationNode
{
    /// <summary>
    /// Gets or sets the node.
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// Gets or sets the requested operation.
    /// </summary>
    public required GraphMutationNodeOperation Operation { get; init; }

    /// <summary>
    /// Gets or sets the optional business key.
    /// </summary>
    public string? Key { get; init; }
}