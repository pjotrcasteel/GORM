using System.Linq.Expressions;

namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph filter step.
/// </summary>
public sealed class GraphFilterStep : GraphQueryStep
{
    /// <summary>
    /// Gets the filter predicate expression.
    /// </summary>
    public required LambdaExpression Predicate { get; init; }
    /// <summary>
    /// Gets whether this filter was applied after a projection.
    /// </summary>
    public bool IsProjected { get; init; }
}