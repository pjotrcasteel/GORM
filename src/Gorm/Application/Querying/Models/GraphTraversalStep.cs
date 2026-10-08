using System.Linq.Expressions;

namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph traversal step.
/// </summary>
public sealed class GraphTraversalStep : GraphQueryStep
{
    /// <summary>
    /// Gets or sets the direction.
    /// </summary>
    public required GraphTraversalDirection Direction { get; init; }
    /// <summary>
    /// Gets or sets the edge type.
    /// </summary>
    public required Type EdgeType { get; init; }
    /// <summary>
    /// Gets or sets the edge predicate.
    /// </summary>
    public LambdaExpression? EdgePredicate { get; init; }
    /// <summary>
    /// Gets or sets the safety.
    /// </summary>
    public GraphTraversalSafeties Safety { get; init; }
}