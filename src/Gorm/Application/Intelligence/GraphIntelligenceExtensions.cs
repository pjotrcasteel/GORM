using Gorm.Application.Intelligence.Algorithms.Centrality;
using Gorm.Application.Intelligence.Compute;
using Gorm.Application.Intelligence.Incremental;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence;

/// <summary>
/// Exposes built-in graph intelligence algorithms.
/// </summary>
public static class GraphIntelligenceExtensions
{
    /// <summary>
    /// Executes a custom vertex-centric compute program.
    /// </summary>
    public static GraphComputeResult<TState> Compute<TState, TMessage>(
        this GraphProjection projection,
        IGraphComputeProgram<TState, TMessage> program,
        GraphComputeOptions? options = null,
        Func<TMessage, TMessage, TMessage>? messageReducer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphComputeAlgorithm<TState, TMessage>(program, options, messageReducer), cancellationToken);
    }

    /// <summary>
    /// Calculates PageRank scores for all projected nodes.
    /// </summary>
    public static GraphPageRankResult PageRank(this GraphProjection projection, GraphPageRankOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphPageRankAlgorithm(options), cancellationToken);
    }

    /// <summary>
    /// Calculates incoming, outgoing and total degree centrality.
    /// </summary>
    public static GraphDegreeCentralityResult DegreeCentrality(this GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphDegreeCentralityAlgorithm(), cancellationToken);
    }

    /// <summary>
    /// Calculates incoming, outgoing and total degree centrality with deterministic partitioning options.
    /// </summary>
    public static GraphDegreeCentralityResult DegreeCentrality(this GraphProjection projection, GraphDegreeCentralityOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(options);
        return projection.Run(new GraphDegreeCentralityAlgorithm(options), cancellationToken);
    }

    /// <summary>
    /// Calculates degree centrality and associates it with an exact projection snapshot version for future safe reuse.
    /// </summary>
    public static GraphVersionedAlgorithmResult<GraphDegreeCentralityResult> DegreeCentralityVersioned(
        this GraphProjectionSnapshot snapshot,
        GraphDegreeCentralityOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new GraphIncrementalDegreeCentralityAlgorithm(options).Execute(snapshot, cancellationToken);
    }

    /// <summary>
    /// Reuses unaffected degree values from an exact base version or safely falls back to full recomputation.
    /// </summary>
    public static GraphIncrementalAlgorithmUpdate<GraphDegreeCentralityResult> UpdateDegreeCentrality(
        this GraphProjectionUpdateResult projectionUpdate,
        GraphVersionedAlgorithmResult<GraphDegreeCentralityResult> previous,
        GraphDegreeCentralityOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectionUpdate);
        ArgumentNullException.ThrowIfNull(previous);
        return new GraphIncrementalDegreeCentralityAlgorithm(options).Update(previous, projectionUpdate, cancellationToken);
    }

    /// <summary>
    /// Calculates exact weighted or unweighted betweenness centrality.
    /// </summary>
    public static GraphBetweennessCentralityResult BetweennessCentrality(
        this GraphProjection projection,
        GraphBetweennessCentralityOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphBetweennessCentralityAlgorithm(options), cancellationToken);
    }
}