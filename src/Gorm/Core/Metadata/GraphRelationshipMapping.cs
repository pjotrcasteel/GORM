using Gorm.Application.Querying.Models;

namespace Gorm.Core.Metadata;

/// <summary>
/// Represents graph relationship mapping.
/// </summary>
public sealed class GraphRelationshipMapping
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the owner node type.
    /// </summary>
    public required Type OwnerNodeType { get; init; }
    /// <summary>
    /// Gets or sets the related node type.
    /// </summary>
    public required Type RelatedNodeType { get; init; }
    /// <summary>
    /// Gets or sets the edge type.
    /// </summary>
    public required Type EdgeType { get; init; }
    /// <summary>
    /// Gets or sets the direction.
    /// </summary>
    public required GraphTraversalDirection Direction { get; init; }
    /// <summary>
    /// Gets many.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphRelationshipMultiplicity Multiplicity { get; init; } = GraphRelationshipMultiplicity.Many;

    /// <summary>
    /// Executes to string.
    /// </summary>
    /// <returns>The value.</returns>
    public override string ToString()
    {
        var arrow = Direction == GraphTraversalDirection.Outgoing
            ? $"--{EdgeType.Name}-->"
            : $"<--{EdgeType.Name}--";

        return $"{OwnerNodeType.Name} {arrow} {RelatedNodeType.Name} ({Name})";
    }
}