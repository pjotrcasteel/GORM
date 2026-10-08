namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Represents a projection cache operation that could not complete without risking stale data.
/// </summary>
public sealed class GraphProjectionCacheException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphProjectionCacheException"/> class.
    /// </summary>
    public GraphProjectionCacheException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphProjectionCacheException"/> class.
    /// </summary>
    public GraphProjectionCacheException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphProjectionCacheException"/> class.
    /// </summary>
    public GraphProjectionCacheException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a projection cache exception.
    /// </summary>
    public GraphProjectionCacheException(GraphProjectionCacheFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the machine-readable failure reason.
    /// </summary>
    public GraphProjectionCacheFailureReason Reason { get; }
}