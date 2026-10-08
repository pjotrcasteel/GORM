using Gorm.Core.Primitives;

namespace Gorm.Application.Mutations.Nodes;

/// <summary>
/// Represents a planned node mutation item.
/// </summary>
public sealed class GraphMutationNodePlanItem
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
    /// Gets or sets the planned action.
    /// </summary>
    public required GraphMutationPlanAction Action { get; init; }

    /// <summary>
    /// Gets or sets the optional business key.
    /// </summary>
    public string? Key { get; init; }
}