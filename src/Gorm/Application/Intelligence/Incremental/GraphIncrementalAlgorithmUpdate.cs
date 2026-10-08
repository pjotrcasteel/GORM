namespace Gorm.Application.Intelligence.Incremental;

/// <summary>
/// Describes a versioned incremental algorithm update and its reuse evidence.
/// </summary>
public sealed class GraphIncrementalAlgorithmUpdate<TResult>
{
    /// <summary>
    /// Gets the new versioned result.
    /// </summary>
    public required GraphVersionedAlgorithmResult<TResult> VersionedResult { get; init; }

    /// <summary>
    /// Gets whether prior work was reused or the algorithm was fully recomputed.
    /// </summary>
    public required GraphIncrementalAlgorithmUpdateMode Mode { get; init; }

    /// <summary>
    /// Gets the number of node-local values reused from the prior version.
    /// </summary>
    public required int ReusedNodeCount { get; init; }

    /// <summary>
    /// Gets the number of node-local values recomputed for the new version.
    /// </summary>
    public required int RecomputedNodeCount { get; init; }

    /// <summary>
    /// Gets a reproducible explanation of why reuse was or was not selected.
    /// </summary>
    public required string Explanation { get; init; }
}