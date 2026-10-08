namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Represents a point-in-time projection cache diagnostics snapshot.
/// </summary>
public sealed class GraphProjectionCacheStatistics
{
    /// <summary>
    /// Gets the current number of cached keys.
    /// </summary>
    public required int EntryCount { get; init; }

    /// <summary>
    /// Gets the number of cache hits.
    /// </summary>
    public required long Hits { get; init; }

    /// <summary>
    /// Gets the number of caller lookups that initially missed.
    /// </summary>
    public required long Misses { get; init; }

    /// <summary>
    /// Gets the number of source load factories started.
    /// </summary>
    public required long Loads { get; init; }

    /// <summary>
    /// Gets the number of callers that joined an already-running source load.
    /// </summary>
    public required long CoalescedLoads { get; init; }

    /// <summary>
    /// Gets the number of entries removed after their time-to-live elapsed.
    /// </summary>
    public required long Expirations { get; init; }

    /// <summary>
    /// Gets the number of explicit invalidations that affected an entry or in-flight load.
    /// </summary>
    public required long Invalidations { get; init; }

    /// <summary>
    /// Gets the number of least-recently-used capacity evictions.
    /// </summary>
    public required long Evictions { get; init; }

    /// <summary>
    /// Gets the number of deltas published incrementally into cached snapshots.
    /// </summary>
    public required long IncrementalUpdates { get; init; }

    /// <summary>
    /// Gets the number of full rebuilds published by cache delta operations.
    /// </summary>
    public required long FullRebuilds { get; init; }

    /// <summary>
    /// Gets the number of source snapshots rejected as missing, wrong-key or stale.
    /// </summary>
    public required long RejectedLoads { get; init; }
}