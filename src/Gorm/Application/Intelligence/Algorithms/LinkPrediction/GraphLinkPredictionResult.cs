namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// Represents a bounded explainable link-prediction result.
/// </summary>
public sealed class GraphLinkPredictionResult
{
    internal GraphLinkPredictionResult(IReadOnlyList<GraphLinkPrediction> predictions, int evaluatedCandidatePairs)
    {
        Predictions = predictions;
        EvaluatedCandidatePairs = evaluatedCandidatePairs;
    }

    /// <summary>
    /// Gets predictions ordered by descending confidence.
    /// </summary>
    public IReadOnlyList<GraphLinkPrediction> Predictions { get; }

    /// <summary>
    /// Gets the number of missing pairs evaluated before evidence and confidence filtering.
    /// </summary>
    public int EvaluatedCandidatePairs { get; }
}