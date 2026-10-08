namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Represents a live subscription stopped by its explicit change safety limit.
/// </summary>
public sealed class GraphLiveChangeLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLiveChangeLimitException"/> class.
    /// </summary>
    public GraphLiveChangeLimitException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLiveChangeLimitException"/> class.
    /// </summary>
    public GraphLiveChangeLimitException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphLiveChangeLimitException"/> class.
    /// </summary>
    public GraphLiveChangeLimitException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a live change limit exception.
    /// </summary>
    public GraphLiveChangeLimitException(int limit)
        : base($"Live projection subscription exceeded its configured limit of {limit} change(s). Start a new subscription from the last committed cursor.")
    {
        Limit = limit;
    }

    /// <summary>
    /// Gets the configured maximum change count.
    /// </summary>
    public int Limit { get; }
}