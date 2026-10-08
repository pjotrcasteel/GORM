namespace Gorm.Application.Intelligence.Compute;

/// <summary>
/// Represents a compute superstep that exceeded its configured message safety limit.
/// </summary>
public sealed class GraphComputeMessageLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphComputeMessageLimitException"/> class.
    /// </summary>
    public GraphComputeMessageLimitException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphComputeMessageLimitException"/> class.
    /// </summary>
    public GraphComputeMessageLimitException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphComputeMessageLimitException"/> class.
    /// </summary>
    public GraphComputeMessageLimitException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes the exception.
    /// </summary>
    public GraphComputeMessageLimitException(int superstep, int limit)
        : base($"Graph compute superstep '{superstep}' exceeded its message limit of '{limit}'.")
    {
        Superstep = superstep;
        Limit = limit;
    }

    /// <summary>
    /// Gets the superstep that exceeded the limit.
    /// </summary>
    public int Superstep { get; }

    /// <summary>
    /// Gets the configured message limit.
    /// </summary>
    public int Limit { get; }
}