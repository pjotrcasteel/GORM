using System.Linq.Expressions;
using Gorm.Application.Querying.Models;

namespace Gorm.Core.Paths;

/// <summary>
/// Represents graph path step.
/// </summary>
public sealed record GraphPathStep
{
    /// <summary>
    /// Gets or sets the direction.
    /// </summary>
    public required GraphPathDirection Direction { get; init; }

    /// <summary>
    /// Gets or sets the edge type.
    /// </summary>
    public required Type EdgeType { get; init; }

    /// <summary>
    /// Gets or sets the node type.
    /// </summary>
    public required Type NodeType { get; init; }

    /// <summary>
    /// Gets or sets the edge predicate.
    /// </summary>
    public LambdaExpression? EdgePredicate { get; init; }

    /// <summary>
    /// Gets or sets the safety.
    /// </summary>
    public GraphTraversalSafeties Safety { get; init; }
}