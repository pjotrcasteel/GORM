using System.Globalization;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Algorithms.Pathfinding;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Compares a baseline route with bounded optimal alternatives after node and edge failures.
/// </summary>
public sealed class GraphImpactReroutingAlgorithm : IGraphAlgorithm<GraphImpactReroutingResult>
{
    private readonly Guid _sourceNodeId;
    private readonly Guid _destinationNodeId;
    private readonly GraphImpactReroutingOptions _options;

    /// <summary>
    /// Initializes an impact-aware rerouting algorithm.
    /// </summary>
    public GraphImpactReroutingAlgorithm(Guid sourceNodeId, Guid destinationNodeId, GraphImpactReroutingOptions? options = null)
    {
        _sourceNodeId = sourceNodeId;
        _destinationNodeId = destinationNodeId;
        _options = options ?? new GraphImpactReroutingOptions();
        GraphPathSearch.ValidateDirection(_options.Direction);

        if (_options.MaximumAlternativePaths <= 0 || _options.MaximumSpurSearches <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Rerouting path and spur-search limits must be positive.");
        }
    }

    /// <inheritdoc />
    public GraphImpactReroutingResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        ValidateFailureIdentifiers(projection);
        var sourceIndex = projection.GetNodeIndex(_sourceNodeId);
        var destinationIndex = projection.GetNodeIndex(_destinationNodeId);
        var edgeConfiguration = GraphPathSearch.CreateEdgeConfiguration(projection, _options.CostSelector, _options.EdgePredicate, cancellationToken);
        var baseline = GraphPathSearch.FindShortestPath(
            new GraphPathSearch.FindShortestPathParameters
            {
                Projection = projection,
                StartIndex = sourceIndex,
                DestinationIndex = destinationIndex,
                Direction = _options.Direction,
                Edges = edgeConfiguration,
                Heuristic = _options.Heuristic,
                BannedNodes = null,
                BannedEdges = null,
                CancellationToken = cancellationToken
            },
            out _
        );
        var baselinePath = baseline is null
            ? null
            : GraphPathSearch.ToPublicPath(
                projection,
                baseline,
                string.Create(CultureInfo.InvariantCulture, $"Pre-impact optimal route cost {baseline.TotalCost:F3} across {baseline.EdgeIndices.Length} hop(s)."));

        if (_options.FailedNodeIds.Contains(_sourceNodeId) || _options.FailedNodeIds.Contains(_destinationNodeId))
        {
            return CreateUnavailableResult(baselinePath, "No reroute is possible because the source or destination node is marked failed.");
        }

        var failedNodeIds = _options.FailedNodeIds;
        var failedEdgeIds = _options.FailedEdgeIds;
        bool AvailableEdge(Core.Primitives.Edge edge) =>
            (_options.EdgePredicate?.Invoke(edge) ?? true) && !failedEdgeIds.Contains(edge.Id) && !failedNodeIds.Contains(edge.FromId) && !failedNodeIds.Contains(edge.ToId);

        var alternativesResult = new GraphYenKShortestPathsAlgorithm(
            _sourceNodeId,
            _destinationNodeId,
            new GraphKShortestPathsOptions
            {
                Direction = _options.Direction,
                CostSelector = _options.CostSelector,
                EdgePredicate = AvailableEdge,
                MaximumPaths = _options.MaximumAlternativePaths,
                MaximumSpurSearches = _options.MaximumSpurSearches
            }).Execute(projection, cancellationToken);
        if (alternativesResult.Paths.Count == 0)
        {
            return CreateUnavailableResult(
                baselinePath,
                $"No route remains after excluding {_options.FailedNodeIds.Count} failed node(s) and " +
                $"{_options.FailedEdgeIds.Count} failed edge(s).");
        }

        var recommendation = alternativesResult.Paths[0];
        double? additionalCost = baselinePath is null ? null : recommendation.TotalCost - baselinePath.TotalCost;
        int? additionalHops = baselinePath is null ? null : recommendation.HopCount - baselinePath.HopCount;
        return new GraphImpactReroutingResult
        {
            BaselinePath = baselinePath,
            RecommendedPath = recommendation,
            AlternativePaths = alternativesResult.Paths,
            AdditionalCost = additionalCost,
            AdditionalHopCount = additionalHops,
            Explanation = baselinePath is null
                ? $"No pre-impact route existed; {alternativesResult.Paths.Count} post-impact route(s) were found."
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "The recommended route avoids {0} failed node(s) and {1} failed edge(s); cost changes by {2:F3} and hops by {3}.",
                    _options.FailedNodeIds.Count,
                    _options.FailedEdgeIds.Count,
                    additionalCost,
                    additionalHops)
        };
    }

    private static GraphImpactReroutingResult CreateUnavailableResult(GraphRoutePath? baseline, string explanation) =>
        new()
        {
            BaselinePath = baseline,
            RecommendedPath = null,
            AlternativePaths = [],
            AdditionalCost = null,
            AdditionalHopCount = null,
            Explanation = explanation
        };

    private void ValidateFailureIdentifiers(GraphProjection projection)
    {
        var unknownNodes = _options.FailedNodeIds.Where(nodeId => !projection.ContainsNode(nodeId)).Take(1).ToArray();

        if (unknownNodes.Length > 0)
        {
            throw new InvalidOperationException($"Failed node '{unknownNodes[0]}' is not part of the {nameof(projection)}.");
        }

        var projectedEdgeIds = projection.Edges.Select(edge => edge.Id).ToHashSet();

        var unknownEdges = _options.FailedEdgeIds.Where(edgeId => !projectedEdgeIds.Contains(edgeId)).Take(1).ToArray();

        if (unknownEdges.Length > 0)
        {
            throw new InvalidOperationException($"Failed edge '{unknownEdges[0]}' is not part of the {nameof(projection)}.");
        }
    }
}