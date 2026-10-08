using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// Configures explainable topological link prediction.
/// </summary>
public sealed class GraphLinkPredictionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether source-to-destination direction is preserved.
    /// Directed predictions use source successors and destination predecessors as evidence.
    /// </summary>
    public bool Directed { get; set; }

    /// <summary>
    /// Gets or sets an optional predicate selecting edges used as topology evidence.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets or sets an optional predicate selecting nodes that may participate in candidates.
    /// </summary>
    public Func<Node, bool>? CandidateNodePredicate { get; set; }

    /// <summary>
    /// Gets or sets an optional predicate selecting source/target candidate pairs.
    /// </summary>
    public Func<Node, Node, bool>? CandidatePairPredicate { get; set; }

    /// <summary>
    /// Gets or sets the optional community-based candidate scope.
    /// </summary>
    public GraphLinkPredictionCandidateScope CandidateScope { get; set; } = GraphLinkPredictionCandidateScope.All;

    /// <summary>
    /// Gets or sets the maximum number of returned predictions.
    /// </summary>
    public int MaximumResults { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of missing candidate pairs that may be evaluated.
    /// </summary>
    public int MaximumCandidatePairs { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets the minimum number of shared topology-evidence nodes.
    /// </summary>
    public int MinimumCommonNeighbors { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum combined confidence in the range zero through one.
    /// </summary>
    public double MinimumConfidence { get; set; }

    /// <summary>
    /// Gets or sets the normalized common-neighbours metric weight.
    /// </summary>
    public double CommonNeighborsWeight { get; set; } = 1;

    /// <summary>
    /// Gets or sets the Jaccard metric weight.
    /// </summary>
    public double JaccardWeight { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the normalized Adamic-Adar metric weight.
    /// </summary>
    public double AdamicAdarWeight { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the normalized preferential-attachment metric weight.
    /// </summary>
    public double PreferentialAttachmentWeight { get; set; } = 0.25;

    /// <summary>
    /// Gets or sets the same-community affinity weight when a community result is supplied.
    /// </summary>
    public double CommunityAffinityWeight { get; set; } = 1;
}