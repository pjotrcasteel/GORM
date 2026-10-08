namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Represents a duplicate or regressing provider change-feed position.
/// </summary>
public sealed class GraphChangeFeedOrderException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphChangeFeedOrderException"/> class.
    /// </summary>
    public GraphChangeFeedOrderException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphChangeFeedOrderException"/> class.
    /// </summary>
    public GraphChangeFeedOrderException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphChangeFeedOrderException"/> class.
    /// </summary>
    public GraphChangeFeedOrderException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a change-feed ordering exception.
    /// </summary>
    public GraphChangeFeedOrderException(long previousSequence, long receivedSequence)
        : base($"Change-feed sequence {receivedSequence} must be greater than previously committed sequence {previousSequence}.")
    {
        PreviousSequence = previousSequence;
        ReceivedSequence = receivedSequence;
    }

    /// <summary>
    /// Gets the last successfully committed sequence.
    /// </summary>
    public long PreviousSequence { get; }

    /// <summary>
    /// Gets the invalid received sequence.
    /// </summary>
    public long ReceivedSequence { get; }
}