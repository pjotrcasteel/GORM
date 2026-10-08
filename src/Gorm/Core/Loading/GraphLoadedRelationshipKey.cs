using System.Runtime.CompilerServices;
using Gorm.Application.Querying.Models;

namespace Gorm.Core.Loading;

/// <summary>
/// Represents graph loaded relationship key.
/// </summary>
public sealed class GraphLoadedRelationshipKey : IEquatable<GraphLoadedRelationshipKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLoadedRelationshipKey"/> class.
    /// </summary>
    /// <param name="owner">The owner node.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="edgeType">The edge type.</param>
    /// <param name="nodeType">The node type.</param>
    /// <param name="includeEdge">The include edge.</param>
    public GraphLoadedRelationshipKey(object owner, GraphTraversalDirection direction, Type edgeType, Type nodeType, bool includeEdge)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Direction = direction;
        EdgeType = edgeType ?? throw new ArgumentNullException(nameof(edgeType));
        NodeType = nodeType ?? throw new ArgumentNullException(nameof(nodeType));
        IncludeEdge = includeEdge;
    }

    /// <summary>
    /// Gets or sets the owner.
    /// </summary>
    public object Owner { get; }

    /// <summary>
    /// Gets or sets the direction.
    /// </summary>
    public GraphTraversalDirection Direction { get; }

    /// <summary>
    /// Gets or sets the edge type.
    /// </summary>
    public Type EdgeType { get; }

    /// <summary>
    /// Gets or sets the node type.
    /// </summary>
    public Type NodeType { get; }

    /// <summary>
    /// Gets or sets the include edge.
    /// </summary>
    public bool IncludeEdge { get; }

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool Equals(GraphLoadedRelationshipKey? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(Owner, other.Owner) && Direction == other.Direction && EdgeType == other.EdgeType && NodeType == other.NodeType && IncludeEdge == other.IncludeEdge;
    }

    /// <summary>
    /// Executes equals.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public override bool Equals(object? obj) => Equals(obj as GraphLoadedRelationshipKey);

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <returns>The value.</returns>
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Owner), Direction, EdgeType, NodeType, IncludeEdge);
}