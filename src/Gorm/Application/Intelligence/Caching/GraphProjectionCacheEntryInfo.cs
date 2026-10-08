using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Exposes read-only diagnostics for one cached projection without exposing mutable cache state.
/// </summary>
public sealed class GraphProjectionCacheEntryInfo
{
    /// <summary>
    /// Gets the explicit projection key.
    /// </summary>
    public required GraphProjectionKey Key { get; init; }

    /// <summary>
    /// Gets the cached source version.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Gets when the cache entry expires.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the number of projected nodes.
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// Gets the number of projected edges.
    /// </summary>
    public required int EdgeCount { get; init; }
}