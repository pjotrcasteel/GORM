namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// The exception thrown when link prediction exceeds its candidate-pair safety limit.
/// </summary>
public sealed class GraphLinkPredictionCandidateLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLinkPredictionCandidateLimitException"/> class.
    /// </summary>
    public GraphLinkPredictionCandidateLimitException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLinkPredictionCandidateLimitException"/> class.
    /// </summary>
    public GraphLinkPredictionCandidateLimitException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLinkPredictionCandidateLimitException"/> class.
    /// </summary>
    public GraphLinkPredictionCandidateLimitException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    internal GraphLinkPredictionCandidateLimitException(int limit)
        : base(
            $"Link prediction exceeded its limit of {limit} missing candidate pairs. " +
            "Narrow CandidateNodePredicate, CandidatePairPredicate or CandidateScope, or explicitly raise the limit.")
    {
        Limit = limit;
    }

    /// <summary>
    /// Gets the configured candidate-pair limit.
    /// </summary>
    public int Limit { get; }
}