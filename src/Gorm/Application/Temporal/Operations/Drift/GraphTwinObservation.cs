using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Binds one monotonic live cursor to expected and independently observed worlds.
/// </summary>
public sealed class GraphTwinObservation
{
    /// <summary>
    /// Gets the strictly increasing live sequence.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Gets the UTC instant at which observation was received.
    /// </summary>
    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>
    /// Gets the expected twin state.
    /// </summary>
    public required GraphWorldSnapshot Expected { get; init; }

    /// <summary>
    /// Gets the actual observed state.
    /// </summary>
    public required GraphWorldSnapshot Actual { get; init; }
}