using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Caching;

/// <summary>
/// Describes a version-checked graph projection cache lookup.
/// </summary>
public sealed class GraphProjectionCacheResult
{
    /// <summary>
    /// Gets the projection snapshot that satisfied the requested minimum version.
    /// </summary>
    public required GraphProjectionSnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets whether the result was a hit, initial load or refresh.
    /// </summary>
    public required GraphProjectionCacheResultStatus Status { get; init; }

    /// <summary>
    /// Gets whether this caller shared an already-running load for the same key.
    /// </summary>
    public required bool WasCoalesced { get; init; }
}