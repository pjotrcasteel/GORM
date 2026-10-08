using Gorm.Application.Context;
using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Core.Sets;

/// <summary>
/// Represents graph set.
/// </summary>
public sealed class GraphSet<TNode> : GraphSetBase<TNode, NodeTypeMapping>
    where TNode : Node
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphSetBase{TEntity, TMapping}"/> class.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="mapping">The mapping.</param>
    internal GraphSet(GraphContext context, NodeTypeMapping mapping)
        : base(context, mapping, GraphQueryElementKind.Node)
    {
    }
}