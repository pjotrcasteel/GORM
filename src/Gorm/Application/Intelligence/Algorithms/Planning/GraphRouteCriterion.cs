using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Defines one additive metric used for Pareto route decisions.
/// </summary>
public sealed class GraphRouteCriterion
{
    /// <summary>
    /// Gets the unique metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the finite non-negative edge-value selector.
    /// </summary>
    public required Func<Edge, double> Selector { get; init; }

    /// <summary>
    /// Gets whether accumulated values are minimized or maximized.
    /// </summary>
    public GraphOptimizationGoal Goal { get; init; } = GraphOptimizationGoal.Minimize;

    /// <summary>
    /// Gets the non-negative weight used only to rank routes already on the Pareto frontier.
    /// </summary>
    public double RankingWeight { get; init; } = 1;
}