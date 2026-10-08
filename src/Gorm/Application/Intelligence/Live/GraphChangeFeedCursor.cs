namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Represents a monotonic provider-neutral change-feed position.
/// </summary>
public readonly record struct GraphChangeFeedCursor
{
    /// <summary>
    /// Initializes a change-feed cursor.
    /// </summary>
    public GraphChangeFeedCursor(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        Sequence = sequence;
    }

    /// <summary>
    /// Gets the monotonic sequence last processed successfully.
    /// </summary>
    public long Sequence { get; }
}