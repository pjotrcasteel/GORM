namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Configures bounded graph projection caching.
/// </summary>
public sealed class GraphProjectionCacheOptions
{
    /// <summary>
    /// Gets or sets absolute time-to-live for a cached snapshot. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable expiry.
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum number of cached projection keys.
    /// </summary>
    public int MaximumEntries { get; set; } = 128;

    /// <summary>
    /// Gets or sets how often a caller may retry after a concurrent invalidation or an insufficient coalesced load.
    /// </summary>
    public int MaximumRefreshAttempts { get; set; } = 4;
}