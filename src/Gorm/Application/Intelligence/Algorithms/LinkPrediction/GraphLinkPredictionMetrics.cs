namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// Represents independently inspectable link-prediction metrics.
/// </summary>
public sealed class GraphLinkPredictionMetrics
{
    /// <summary>
    /// Gets the number of shared evidence nodes.
    /// </summary>
    public required int CommonNeighbors { get; init; }

    /// <summary>
    /// Gets shared-neighbour intersection divided by union.
    /// </summary>
    public required double Jaccard { get; init; }

    /// <summary>
    /// Gets the Adamic-Adar score, favouring rarer shared evidence nodes.
    /// </summary>
    public required double AdamicAdar { get; init; }

    /// <summary>
    /// Gets source evidence-degree multiplied by target evidence-degree.
    /// </summary>
    public required double PreferentialAttachment { get; init; }

    /// <summary>
    /// Gets 1 when both candidates belong to the same community, otherwise 0.
    /// This value is 0 when no community result was supplied.
    /// </summary>
    public required double CommunityAffinity { get; init; }
}