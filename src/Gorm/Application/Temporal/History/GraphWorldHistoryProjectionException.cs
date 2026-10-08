namespace Gorm.Application.Temporal.History;

/// <summary>
/// Represents a classified temporal history projection failure.
/// </summary>
#pragma warning disable S3871
public sealed class GraphWorldHistoryProjectionException : InvalidOperationException
{
    /// <summary>
    /// Initializes a temporal history projection failure.
    /// </summary>
    /// <param name="reason">Machine-readable failure reason.</param>
    /// <param name="message">Failure explanation.</param>
    public GraphWorldHistoryProjectionException(GraphWorldHistoryProjectionFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the machine-readable failure reason.
    /// </summary>
    public GraphWorldHistoryProjectionFailureReason Reason { get; }
}
#pragma warning restore S3871