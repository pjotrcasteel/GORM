using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Runtime;

/// <summary>
/// Represents one live algorithm result tied to its committed change cursor and snapshot.
/// </summary>
public sealed class GraphLiveAlgorithmUpdate
{
    /// <summary>
    /// Gets the committed provider cursor.
    /// </summary>
    public required GraphChangeFeedCursor Cursor { get; init; }

    /// <summary>
    /// Gets the incremental or rebuild projection publication.
    /// </summary>
    public required GraphProjectionUpdateResult ProjectionUpdate { get; init; }

    /// <summary>
    /// Gets the algorithm result with exact snapshot provenance.
    /// </summary>
    public required GraphAlgorithmExecutionResult Execution { get; init; }

    /// <summary>
    /// Gets the UTC instant at which runtime execution completed.
    /// </summary>
    public required DateTimeOffset ProcessedAt { get; init; }
}