using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Incremental;

/// <summary>
/// Reuses unaffected raw degree values and recomputes only nodes whose value or adjacency changed.
/// </summary>
public sealed class GraphIncrementalDegreeCentralityAlgorithm : IGraphIncrementalAlgorithm<GraphDegreeCentralityResult>
{
    private const string AlgorithmIdentifier = "degree-centrality:v1";
    private readonly GraphDegreeCentralityOptions _options;

    /// <summary>
    /// Initializes an incremental degree centrality algorithm.
    /// </summary>
    public GraphIncrementalDegreeCentralityAlgorithm(GraphDegreeCentralityOptions? options = null)
    {
        _options = options ?? new GraphDegreeCentralityOptions();
    }

    /// <inheritdoc />
    public string Name => AlgorithmIdentifier;

    /// <inheritdoc />
    public GraphVersionedAlgorithmResult<GraphDegreeCentralityResult> Execute(GraphProjectionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var calculation = GraphDegreeCentralityCalculator.Calculate(snapshot.Projection, _options, previousDegrees: null, affectedNodeIds: null, cancellationToken);
        return CreateVersionedResult(snapshot, calculation);
    }

    /// <inheritdoc />
    public GraphIncrementalAlgorithmUpdate<GraphDegreeCentralityResult> Update(
        GraphVersionedAlgorithmResult<GraphDegreeCentralityResult> previous,
        GraphProjectionUpdateResult projectionUpdate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(projectionUpdate);

        if (previous.AlgorithmName != Name)
        {
            throw new ArgumentException(
                $"Versioned result '{previous.AlgorithmName}' cannot be updated by algorithm '{Name}'.",
                nameof(previous));
        }

        var snapshot = projectionUpdate.Snapshot;
        if (previous.Key != snapshot.Key)
        {
            throw new ArgumentException(
                $"Previous result key '{previous.Key}' does not match updated projection key '{snapshot.Key}'.",
                nameof(previous));
        }

        if (previous.Version >= snapshot.Version)
        {
            throw new ArgumentException(
                $"Previous result version {previous.Version} must be older than updated version {snapshot.Version}.",
                nameof(previous));
        }

        var canReuse =
            projectionUpdate.Mode == GraphProjectionUpdateMode.Incremental &&
            previous.Version == projectionUpdate.BaseVersion &&
            previous.IncrementalState is IReadOnlyDictionary<Guid, GraphRawDegree>;
        var calculation = canReuse
            ? GraphDegreeCentralityCalculator.Calculate(
                snapshot.Projection,
                _options,
                (IReadOnlyDictionary<Guid, GraphRawDegree>)previous.IncrementalState!,
                projectionUpdate.AffectedNodeIds.ToHashSet(),
                cancellationToken)
            : GraphDegreeCentralityCalculator.Calculate(snapshot.Projection, _options, previousDegrees: null, affectedNodeIds: null, cancellationToken);

        return new GraphIncrementalAlgorithmUpdate<GraphDegreeCentralityResult>
        {
            VersionedResult = CreateVersionedResult(snapshot, calculation),
            Mode = canReuse
                ? GraphIncrementalAlgorithmUpdateMode.Incremental
                : GraphIncrementalAlgorithmUpdateMode.FullRecompute,
            ReusedNodeCount = calculation.ReusedNodeCount,
            RecomputedNodeCount = calculation.RecomputedNodeCount,
            Explanation = canReuse
                ? $"Reused {calculation.ReusedNodeCount} unaffected raw degree value(s) from exact base version {previous.Version}; " +
                  $"recomputed {calculation.RecomputedNodeCount} affected node(s) for version {snapshot.Version}."
                : $"Fully recomputed degree centrality for version {snapshot.Version} because the prior result was not an " +
                  "exact compatible incremental base."
        };
    }

    private GraphVersionedAlgorithmResult<GraphDegreeCentralityResult> CreateVersionedResult(GraphProjectionSnapshot snapshot, GraphDegreeCentralityCalculation calculation) =>
        new(Name, snapshot.Key, snapshot.Version, calculation.Result, calculation.DegreesByNodeId);
}