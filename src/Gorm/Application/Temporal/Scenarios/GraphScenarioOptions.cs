namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Bounds isolated scenario growth and enforces temporal ordering.
/// </summary>
public sealed class GraphScenarioOptions
{
    /// <summary>
    /// Gets or sets the maximum number of scenario mutations.
    /// </summary>
    public int MaximumRevisions { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum nodes in a scenario world.
    /// </summary>
    public int MaximumNodes { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum edges in a scenario world.
    /// </summary>
    public int MaximumEdges { get; init; } = 10_000_000;

    /// <summary>
    /// Gets or sets whether recorded time must be monotonic across revisions.
    /// </summary>
    public bool RequireMonotonicRecordedTime { get; init; } = true;
}