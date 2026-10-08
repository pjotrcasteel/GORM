using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Configures bounded multi-criteria Pareto route search.
/// </summary>
public sealed class GraphParetoRouteOptions
{
    /// <summary>
    /// Gets or sets allowed traversal directions.
    /// </summary>
    public GraphAlgorithmTraversalDirection Direction { get; set; } = GraphAlgorithmTraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets an optional edge availability predicate.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets additive route criteria. At least one criterion is required.
    /// </summary>
    public IList<GraphRouteCriterion> Criteria { get; } = new List<GraphRouteCriterion>();

    /// <summary>
    /// Gets or sets the maximum number of returned ranked Pareto routes.
    /// </summary>
    public int MaximumResults { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum number of labels retained at one node.
    /// Reaching this limit marks the result as truncated.
    /// </summary>
    public int MaximumLabelsPerNode { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the maximum number of expanded labels.
    /// </summary>
    public int MaximumExpandedLabels { get; set; } = 200_000;

    /// <summary>
    /// Gets or sets the maximum number of returned rejected-alternative explanations.
    /// </summary>
    public int MaximumRejectedAlternatives { get; set; } = 100;
}