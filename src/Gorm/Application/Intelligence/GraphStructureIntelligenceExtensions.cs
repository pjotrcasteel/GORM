using Gorm.Application.Intelligence.Algorithms.Communities;
using Gorm.Application.Intelligence.Algorithms.Connectivity;
using Gorm.Application.Intelligence.Algorithms.LinkPrediction;
using Gorm.Application.Intelligence.Algorithms.Planning;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence;

/// <summary>
/// Exposes built-in graph structure and community algorithms.
/// </summary>
public static class GraphStructureIntelligenceExtensions
{
    /// <summary>
    /// Finds weakly or strongly connected components.
    /// </summary>
    public static GraphConnectedComponentsResult ConnectedComponents(
        this GraphProjection projection,
        GraphComponentKind kind = GraphComponentKind.Weak,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphConnectedComponentsAlgorithm(kind), cancellationToken);
    }

    /// <summary>
    /// Enumerates bounded simple directed cycles.
    /// </summary>
    public static GraphCycleDetectionResult DetectCycles(this GraphProjection projection, GraphCycleDetectionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphCycleDetectionAlgorithm(options), cancellationToken);
    }

    /// <summary>
    /// Calculates a Critical Path Method schedule for a directed acyclic projection.
    /// </summary>
    public static GraphCriticalPathResult CriticalPath(this GraphProjection projection, GraphCriticalPathOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphCriticalPathAlgorithm(options), cancellationToken);
    }

    /// <summary>
    /// Detects deterministic hierarchical weighted Leiden communities, boundaries and anomalies.
    /// </summary>
    public static GraphCommunityDetectionResult DetectCommunities(
        this GraphProjection projection,
        GraphCommunityDetectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphLeidenCommunityAlgorithm(options), cancellationToken);
    }

    /// <summary>
    /// Predicts missing links using explainable topology and optional community evidence.
    /// </summary>
    public static GraphLinkPredictionResult PredictLinks(
        this GraphProjection projection,
        GraphLinkPredictionOptions? options = null,
        GraphCommunityDetectionResult? communities = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return projection.Run(new GraphLinkPredictionAlgorithm(options, communities), cancellationToken);
    }
}