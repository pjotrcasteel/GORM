using Gorm.Application.Intelligence.Algorithms.Flow;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Algorithms.Planning;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence;

/// <summary>
/// Exposes built-in graph routing and flow algorithms.
/// </summary>
public static class GraphRoutingIntelligenceExtensions
{
    /// <summary>
    /// Finds an optimal route using A* and an optional admissible heuristic.
    /// </summary>
    public static GraphAStarResult? AStarPath(
        this GraphProjection projection,
        Guid startNodeId,
        Guid destinationNodeId,
        GraphAStarOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphAStarAlgorithm(startNodeId, destinationNodeId, options), cancellationToken);
    }

    /// <summary>
    /// Finds cost-ordered unique loopless alternatives using Yen's algorithm.
    /// </summary>
    public static GraphKShortestPathsResult KShortestPaths(
        this GraphProjection projection,
        Guid startNodeId,
        Guid destinationNodeId,
        GraphKShortestPathsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphYenKShortestPathsAlgorithm(startNodeId, destinationNodeId, options), cancellationToken);
    }

    /// <summary>
    /// Calculates directed maximum flow, minimum cut and saturated bottlenecks.
    /// </summary>
    public static GraphMaximumFlowResult MaximumFlow(
        this GraphProjection projection,
        Guid sourceNodeId,
        Guid destinationNodeId,
        GraphMaximumFlowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphMaximumFlowAlgorithm(sourceNodeId, destinationNodeId, options), cancellationToken);
    }

    /// <summary>
    /// Finds and ranks a bounded multi-criteria Pareto frontier of loopless routes.
    /// </summary>
    public static GraphParetoRouteResult ParetoRoutes(
        this GraphProjection projection,
        Guid sourceNodeId,
        Guid destinationNodeId,
        GraphParetoRouteOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphParetoRouteAlgorithm(sourceNodeId, destinationNodeId, options), cancellationToken);
    }

    /// <summary>
    /// Compares a baseline route with bounded optimal alternatives after topology failures.
    /// </summary>
    public static GraphImpactReroutingResult RerouteAroundImpact(
        this GraphProjection projection,
        Guid sourceNodeId,
        Guid destinationNodeId,
        GraphImpactReroutingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphImpactReroutingAlgorithm(sourceNodeId, destinationNodeId, options), cancellationToken);
    }

    /// <summary>
    /// Finds a weighted shortest path between two projected nodes.
    /// </summary>
    public static GraphShortestPathResult? ShortestPath(
        this GraphProjection projection,
        Guid startNodeId,
        Guid destinationNodeId,
        GraphShortestPathOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphShortestPathAlgorithm(startNodeId, destinationNodeId, options), cancellationToken);
    }
}