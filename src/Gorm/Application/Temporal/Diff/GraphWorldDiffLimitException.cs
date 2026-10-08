namespace Gorm.Application.Temporal.Diff;

/// <summary>
/// Indicates that a temporal world comparison exceeded its explicit change limit.
/// </summary>
#pragma warning disable S3871
public sealed class GraphWorldDiffLimitException : InvalidOperationException
{
    /// <summary>
    /// Initializes a world-difference limit failure.
    /// </summary>
    /// <param name="maximumChanges">Configured maximum number of changes.</param>
    public GraphWorldDiffLimitException(int maximumChanges)
        : base($"World comparison exceeded MaximumChanges ({maximumChanges}). Narrow the compared scope.")
    {
        MaximumChanges = maximumChanges;
    }

    /// <summary>
    /// Gets the configured maximum number of changes.
    /// </summary>
    public int MaximumChanges { get; }
}
#pragma warning restore S3871