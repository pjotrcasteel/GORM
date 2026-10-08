using Gorm.Application.Context;
using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Core.Sets;

/// <summary>
/// Represents graph edge set.
/// </summary>
public sealed class GraphEdgeSet<TEdge> : GraphSetBase<TEdge, EdgeTypeMapping>
    where TEdge : Edge
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphEdgeSet{TEdge}"/> class.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="mapping">The mapping.</param>
    internal GraphEdgeSet(GraphContext context, EdgeTypeMapping mapping)
        : base(context, mapping, GraphQueryElementKind.Edge)
    {
    }
}