namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Contains ordered live observation identity and its drift report.
/// </summary>
public sealed class GraphTwinSynchronizationResult
{
    /// <summary>
    /// Gets the observation sequence.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Gets the normalized UTC observation time.
    /// </summary>
    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>
    /// Gets exact expected-versus-actual drift evidence.
    /// </summary>
    public required GraphTwinDriftReport Drift { get; init; }
}