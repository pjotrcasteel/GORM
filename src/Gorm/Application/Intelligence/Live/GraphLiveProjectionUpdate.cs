using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Represents one successfully committed live projection update.
/// </summary>
public sealed class GraphLiveProjectionUpdate
{
    /// <summary>
    /// Gets the cursor safe to persist after consuming this update.
    /// </summary>
    public required GraphChangeFeedCursor Cursor { get; init; }

    /// <summary>
    /// Gets the atomic incremental or full-rebuild projection result.
    /// </summary>
    public required GraphProjectionUpdateResult ProjectionUpdate { get; init; }

    /// <summary>
    /// Gets when the live engine committed the update.
    /// </summary>
    public required DateTimeOffset ProcessedAt { get; init; }
}