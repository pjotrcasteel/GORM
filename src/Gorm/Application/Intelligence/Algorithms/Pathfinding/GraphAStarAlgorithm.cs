using System.Globalization;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Finds an optimal path using A* and a caller-supplied admissible heuristic.
/// </summary>
public sealed class GraphAStarAlgorithm : IGraphAlgorithm<GraphAStarResult?>
{
    private readonly Guid _startNodeId;
    private readonly Guid _destinationNodeId;
    private readonly GraphAStarOptions _options;

    /// <summary>
    /// Initializes an A* path algorithm.
    /// </summary>
    public GraphAStarAlgorithm(Guid startNodeId, Guid destinationNodeId, GraphAStarOptions? options = null)
    {
        _startNodeId = startNodeId;
        _destinationNodeId = destinationNodeId;
        _options = options ?? new GraphAStarOptions();
        GraphPathSearch.ValidateDirection(_options.Direction);
    }

    /// <inheritdoc />
    public GraphAStarResult? Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var edges = GraphPathSearch.CreateEdgeConfiguration(projection, _options.CostSelector, _options.EdgePredicate, cancellationToken);
        var path = GraphPathSearch.FindShortestPath(
            new GraphPathSearch.FindShortestPathParameters
            {
                Projection = projection,
                StartIndex = projection.GetNodeIndex(_startNodeId),
                DestinationIndex = projection.GetNodeIndex(_destinationNodeId),
                Direction = _options.Direction,
                Edges = edges,
                Heuristic = _options.Heuristic,
                BannedNodes = null,
                BannedEdges = null,
                CancellationToken = cancellationToken
            },
            out var exploredNodeCount
        );

        if (path is null)
        {
            return null;
        }

        return new GraphAStarResult
        {
            Path = GraphPathSearch.ToPublicPath(
                projection,
                path,
                string.Create(CultureInfo.InvariantCulture, $"A* selected the minimum-cost route with cost {path.TotalCost:F3} and {path.EdgeIndices.Length} hop(s).")),
            ExploredNodeCount = exploredNodeCount
        };
    }
}