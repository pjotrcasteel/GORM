using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Wraps one graph delta in a provider-neutral monotonic change-feed position.
/// </summary>
public sealed class GraphProjectionChange
{
    /// <summary>
    /// Initializes a graph projection change.
    /// </summary>
    public GraphProjectionChange(long sequence, GraphProjectionDelta delta)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A change sequence must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(delta);
        Cursor = new GraphChangeFeedCursor(sequence);
        Delta = delta;
    }

    /// <summary>
    /// Gets the position committed after this change is processed successfully.
    /// </summary>
    public GraphChangeFeedCursor Cursor { get; }

    /// <summary>
    /// Gets the ordered graph delta.
    /// </summary>
    public GraphProjectionDelta Delta { get; }
}