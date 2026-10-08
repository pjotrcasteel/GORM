namespace Gorm.Application.Intelligence.Algorithms.Flow;

/// <summary>
/// The exception thrown when maximum-flow execution exceeds its augmenting-path limit.
/// </summary>
public sealed class GraphFlowAugmentationLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphFlowAugmentationLimitException"/> class.
    /// </summary>
    public GraphFlowAugmentationLimitException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphFlowAugmentationLimitException"/> class.
    /// </summary>
    public GraphFlowAugmentationLimitException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphFlowAugmentationLimitException"/> class.
    /// </summary>
    public GraphFlowAugmentationLimitException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    internal GraphFlowAugmentationLimitException(int limit)
        : base($"Maximum-flow execution exceeded its limit of {limit} augmenting paths.")
    {
        Limit = limit;
    }

    /// <summary>
    /// Gets the configured augmentation limit.
    /// </summary>
    public int Limit { get; }
}