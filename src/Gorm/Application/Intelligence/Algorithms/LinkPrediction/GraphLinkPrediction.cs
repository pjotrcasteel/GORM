using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// Represents one explainable missing-link prediction.
/// </summary>
public sealed class GraphLinkPrediction
{
    /// <summary>
    /// Gets the zero-based position after sorting predictions by confidence.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Gets the predicted source node.
    /// </summary>
    public required Node Source { get; init; }

    /// <summary>
    /// Gets the predicted destination node.
    /// </summary>
    public required Node Target { get; init; }

    /// <summary>
    /// Gets combined normalized confidence in the range zero through one.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets independently inspectable raw metrics.
    /// </summary>
    public required GraphLinkPredictionMetrics Metrics { get; init; }

    /// <summary>
    /// Gets ordered shared nodes supporting the prediction.
    /// For directed predictions these form source-to-evidence-to-target paths.
    /// </summary>
    public required IReadOnlyList<Node> EvidenceNodes { get; init; }

    /// <summary>
    /// Gets a human-readable summary of the evidence and combined score.
    /// </summary>
    public required string Explanation { get; init; }
}