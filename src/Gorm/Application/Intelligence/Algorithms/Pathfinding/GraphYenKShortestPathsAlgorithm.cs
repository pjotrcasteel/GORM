using System.Globalization;
using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Finds cost-ordered unique loopless paths using Yen's algorithm.
/// </summary>
public sealed class GraphYenKShortestPathsAlgorithm : IGraphAlgorithm<GraphKShortestPathsResult>
{
    private readonly Guid _startNodeId;
    private readonly Guid _destinationNodeId;
    private readonly GraphKShortestPathsOptions _options;

    /// <summary>
    /// Initializes a Yen K-shortest path algorithm.
    /// </summary>
    public GraphYenKShortestPathsAlgorithm(Guid startNodeId, Guid destinationNodeId, GraphKShortestPathsOptions? options = null)
    {
        _startNodeId = startNodeId;
        _destinationNodeId = destinationNodeId;
        _options = options ?? new GraphKShortestPathsOptions();
        GraphPathSearch.ValidateDirection(_options.Direction);

        if (_options.MaximumPaths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum paths must be greater than zero.");
        }

        if (_options.MaximumSpurSearches <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum spur searches must be greater than zero.");
        }
    }

    /// <inheritdoc />
    public GraphKShortestPathsResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var startIndex = projection.GetNodeIndex(_startNodeId);
        var destinationIndex = projection.GetNodeIndex(_destinationNodeId);
        var edgeConfiguration = GraphPathSearch.CreateEdgeConfiguration(projection, _options.CostSelector, _options.EdgePredicate, cancellationToken);
        var firstPath = FindFirstPath(projection, edgeConfiguration, startIndex, destinationIndex, cancellationToken);

        if (firstPath is null)
        {
            return new GraphKShortestPathsResult([], 0, exhausted: true, truncated: false);
        }

        var search = FindAdditionalPaths(projection, edgeConfiguration, destinationIndex, firstPath, cancellationToken);
        var paths = search.Accepted
            .Select((path, rank) => GraphPathSearch.ToPublicPath(
                projection,
                path,
                string.Create(CultureInfo.InvariantCulture, $"Yen rank {rank + 1}: loopless route cost {path.TotalCost:F3} across {path.EdgeIndices.Length} hop(s).")))
            .ToArray();
        return new GraphKShortestPathsResult(paths, search.SpurSearches, search.Exhausted, search.Truncated);
    }

    private GraphPathSearch.SearchPath? FindFirstPath(
        GraphProjection projection,
        GraphPathSearch.EdgeConfiguration edgeConfiguration,
        int startIndex,
        int destinationIndex,
        CancellationToken cancellationToken) =>
        GraphPathSearch.FindShortestPath(
            new GraphPathSearch.FindShortestPathParameters
            {
                Projection = projection,
                StartIndex = startIndex,
                DestinationIndex = destinationIndex,
                Direction = _options.Direction,
                Edges = edgeConfiguration,
                Heuristic = null,
                BannedNodes = null,
                BannedEdges = null,
                CancellationToken = cancellationToken
            },
            out _
        );

    private PathSearchState FindAdditionalPaths(
        GraphProjection projection,
        GraphPathSearch.EdgeConfiguration edgeConfiguration,
        int destinationIndex,
        GraphPathSearch.SearchPath firstPath,
        CancellationToken cancellationToken)
    {
        var search = new PathSearchState(firstPath);
        while (search.Accepted.Count < _options.MaximumPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GenerateCandidates(projection, edgeConfiguration, destinationIndex, search, cancellationToken);
            if (search.Truncated)
            {
                break;
            }

            var nextPath = DequeueNextPath(search);
            if (nextPath is null)
            {
                search.Exhausted = true;
                break;
            }

            search.Accepted.Add(nextPath);
        }

        return search;
    }

    private void GenerateCandidates(
        GraphProjection projection,
        GraphPathSearch.EdgeConfiguration edgeConfiguration,
        int destinationIndex,
        PathSearchState search,
        CancellationToken cancellationToken)
    {
        var previousPath = search.Accepted[^1];
        var context = new SpurCandidateContext(projection, edgeConfiguration, destinationIndex, search.Accepted, cancellationToken);
        for (var spurOffset = 0; spurOffset < previousPath.NodeIndices.Length - 1; spurOffset++)
        {
            if (search.SpurSearches >= _options.MaximumSpurSearches)
            {
                search.Truncated = true;
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            search.SpurSearches++;
            var candidate = CreateSpurCandidate(context, previousPath, spurOffset);
            if (candidate is null)
            {
                continue;
            }

            var signature = GraphPathSearch.Signature(candidate);
            if (!search.AcceptedSignatures.Contains(signature) &&
                search.QueuedSignatures.Add(signature))
            {
                search.Candidates.Enqueue(candidate, (candidate.TotalCost, signature));
            }
        }
    }

    private GraphPathSearch.SearchPath? CreateSpurCandidate(SpurCandidateContext context, GraphPathSearch.SearchPath previousPath, int spurOffset)
    {
        var rootNodes = previousPath.NodeIndices[..(spurOffset + 1)];
        var rootEdges = previousPath.EdgeIndices[..spurOffset];
        var spurPath = GraphPathSearch.FindShortestPath(
            new GraphPathSearch.FindShortestPathParameters
            {
                Projection = context.Projection,
                StartIndex = rootNodes[^1],
                DestinationIndex = context.DestinationIndex,
                Direction = _options.Direction,
                Edges = context.EdgeConfiguration,
                Heuristic = null,
                BannedNodes = rootNodes[..^1].ToHashSet(),
                BannedEdges = CreateBannedEdges(context.Accepted, rootNodes, rootEdges, spurOffset),
                CancellationToken = context.CancellationToken
            },
            out _);
        if (spurPath is null)
        {
            return null;
        }

        var totalNodes = rootNodes[..^1].Concat(spurPath.NodeIndices).ToArray();
        if (totalNodes.Distinct().Count() != totalNodes.Length)
        {
            return null;
        }

        var totalEdges = rootEdges.Concat(spurPath.EdgeIndices).ToArray();
        var rootCost = rootEdges.Sum(edgeIndex => context.EdgeConfiguration.Costs[edgeIndex]);
        return new GraphPathSearch.SearchPath(totalNodes, totalEdges, rootCost + spurPath.TotalCost);
    }

    private static HashSet<int> CreateBannedEdges(IReadOnlyList<GraphPathSearch.SearchPath> accepted, int[] rootNodes, int[] rootEdges, int spurOffset)
    {
        var bannedEdges = new HashSet<int>();
        foreach (var acceptedPath in accepted)
        {
            if (HasSameRoot(acceptedPath, rootNodes, rootEdges) &&
                acceptedPath.EdgeIndices.Length > spurOffset)
            {
                bannedEdges.Add(acceptedPath.EdgeIndices[spurOffset]);
            }
        }

        return bannedEdges;
    }

    private static GraphPathSearch.SearchPath? DequeueNextPath(PathSearchState search)
    {
        while (search.Candidates.TryDequeue(out var candidate, out _))
        {
            var signature = GraphPathSearch.Signature(candidate);
            search.QueuedSignatures.Remove(signature);
            if (search.AcceptedSignatures.Add(signature))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HasSameRoot(GraphPathSearch.SearchPath path, int[] rootNodes, int[] rootEdges) =>
        path.NodeIndices.Length >= rootNodes.Length &&
        path.EdgeIndices.Length >= rootEdges.Length &&
        path.NodeIndices.Take(rootNodes.Length).SequenceEqual(rootNodes) &&
        path.EdgeIndices.Take(rootEdges.Length).SequenceEqual(rootEdges);

    private sealed record SpurCandidateContext(
        GraphProjection Projection,
        GraphPathSearch.EdgeConfiguration EdgeConfiguration,
        int DestinationIndex,
        IReadOnlyList<GraphPathSearch.SearchPath> Accepted,
        CancellationToken CancellationToken);

    private sealed class PathSearchState(GraphPathSearch.SearchPath firstPath)
    {
        public List<GraphPathSearch.SearchPath> Accepted { get; } = [firstPath];

        public HashSet<string> AcceptedSignatures { get; } =
            [GraphPathSearch.Signature(firstPath)];

        public PriorityQueue<GraphPathSearch.SearchPath, (double Cost, string Signature)> Candidates { get; } =
            new();

        public HashSet<string> QueuedSignatures { get; } = [];

        public int SpurSearches { get; set; }

        public bool Exhausted { get; set; }

        public bool Truncated { get; set; }
    }
}