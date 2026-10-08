namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Represents a safe rejection of a graph projection delta.
/// </summary>
public sealed class GraphProjectionDeltaException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphProjectionDeltaException"/> class.
    /// </summary>
    public GraphProjectionDeltaException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphProjectionDeltaException"/> class.
    /// </summary>
    public GraphProjectionDeltaException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphProjectionDeltaException"/> class.
    /// </summary>
    public GraphProjectionDeltaException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a graph projection delta exception.
    /// </summary>
    public GraphProjectionDeltaException(GraphProjectionDeltaFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Initializes a graph projection delta exception with an inner cause.
    /// </summary>
    public GraphProjectionDeltaException(GraphProjectionDeltaFailureReason reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the machine-readable rejection reason.
    /// </summary>
    public GraphProjectionDeltaFailureReason Reason { get; }
}