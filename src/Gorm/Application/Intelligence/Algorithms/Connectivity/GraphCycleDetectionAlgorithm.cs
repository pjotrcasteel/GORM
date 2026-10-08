using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// Enumerates simple directed cycles with explicit result-count and depth limits.
/// </summary>
public sealed class GraphCycleDetectionAlgorithm : IGraphAlgorithm<GraphCycleDetectionResult>
{
    private readonly GraphCycleDetectionOptions _options;

    /// <summary>
    /// Initializes a bounded cycle-detection algorithm.
    /// </summary>
    public GraphCycleDetectionAlgorithm(GraphCycleDetectionOptions? options = null)
    {
        _options = options ?? new GraphCycleDetectionOptions();

        if (_options.MaximumCycles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum cycles must be greater than zero.");
        }

        if (_options.MaximumDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum depth must be greater than zero.");
        }
    }

    /// <inheritdoc />
    public GraphCycleDetectionResult Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var includedEdges = new bool[projection.Statistics.EdgeCount];
        for (var edgeIndex = 0; edgeIndex < includedEdges.Length; edgeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var edge = projection.GetEdge(edgeIndex);
            includedEdges[edgeIndex] = _options.EdgePredicate?.Invoke(edge) ?? true;
        }

        var cycles = new List<GraphCycle>();
        var visited = new bool[projection.Statistics.NodeCount];
        var nodePath = new List<int>(_options.MaximumDepth);
        var edgePath = new List<int>(_options.MaximumDepth);
        var truncated = false;

        for (var startIndex = 0; startIndex < projection.Statistics.NodeCount; startIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindCycles(
                new FindCyclesParameters
                {
                    Projection = projection,
                    StartIndex = startIndex,
                    CurrentIndex = startIndex,
                    IncludedEdges = includedEdges,
                    Visited = visited,
                    NodePath = nodePath,
                    EdgePath = edgePath,
                    Cycles = cycles,
                    CancellationToken = cancellationToken
                },
                ref truncated
            );

            if (cycles.Count >= _options.MaximumCycles)
            {
                truncated = true;
                break;
            }
        }

        return new GraphCycleDetectionResult(cycles, truncated);
    }

    private void FindCycles(FindCyclesParameters inputs, ref bool truncated)
    {
        var projection = inputs.Projection;
        var currentIndex = inputs.CurrentIndex;
        var visited = inputs.Visited;
        var nodePath = inputs.NodePath;
        var cancellationToken = inputs.CancellationToken;

        cancellationToken.ThrowIfCancellationRequested();
        visited[currentIndex] = true;
        nodePath.Add(currentIndex);

        foreach (var arc in projection.GetOutgoingArcs(currentIndex))
        {
            if (ProcessArc(inputs, arc, ref truncated))
            {
                break;
            }
        }

        nodePath.RemoveAt(nodePath.Count - 1);
        visited[currentIndex] = false;
    }

    private bool ProcessArc(FindCyclesParameters inputs, GraphProjectionArc arc, ref bool truncated)
    {
        if (!inputs.IncludedEdges[arc.EdgeIndex] || arc.NodeIndex < inputs.StartIndex)
        {
            return false;
        }

        if (arc.NodeIndex == inputs.StartIndex)
        {
            truncated |= AddCycle(inputs.Projection, arc.EdgeIndex, inputs.NodePath, inputs.EdgePath, inputs.Cycles);
            return truncated;
        }

        if (inputs.Visited[arc.NodeIndex])
        {
            return false;
        }

        if (inputs.NodePath.Count >= _options.MaximumDepth)
        {
            truncated = true;
            return false;
        }

        inputs.EdgePath.Add(arc.EdgeIndex);
        FindCycles(CreateChildParameters(inputs, arc.NodeIndex), ref truncated);
        inputs.EdgePath.RemoveAt(inputs.EdgePath.Count - 1);
        return inputs.Cycles.Count >= _options.MaximumCycles;
    }

    private static FindCyclesParameters CreateChildParameters(FindCyclesParameters inputs, int currentIndex) =>
        new()
        {
            Projection = inputs.Projection,
            StartIndex = inputs.StartIndex,
            CurrentIndex = currentIndex,
            IncludedEdges = inputs.IncludedEdges,
            Visited = inputs.Visited,
            NodePath = inputs.NodePath,
            EdgePath = inputs.EdgePath,
            Cycles = inputs.Cycles,
            CancellationToken = inputs.CancellationToken
        };

    private bool AddCycle(GraphProjection projection, int edgeIndex, List<int> nodePath, List<int> edgePath, List<GraphCycle> cycles)
    {
        edgePath.Add(edgeIndex);
        cycles.Add(CreateCycle(projection, cycles.Count, nodePath, edgePath));
        edgePath.RemoveAt(edgePath.Count - 1);
        return cycles.Count >= _options.MaximumCycles;
    }

    private static GraphCycle CreateCycle(GraphProjection projection, int id, IEnumerable<int> nodePath, IEnumerable<int> edgePath) =>
        new()
        {
            Id = id,
            Nodes = [.. nodePath.Select(projection.GetNode)],
            Edges = [.. edgePath.Select(projection.GetEdge)]
        };

    private sealed class FindCyclesParameters
    {
        public required GraphProjection Projection { get; init; }

        public required int StartIndex { get; init; }

        public required int CurrentIndex { get; init; }

        public required bool[] IncludedEdges { get; init; }

        public required bool[] Visited { get; init; }

        public required List<int> NodePath { get; init; }

        public required List<int> EdgePath { get; init; }

        public required List<GraphCycle> Cycles { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }
}