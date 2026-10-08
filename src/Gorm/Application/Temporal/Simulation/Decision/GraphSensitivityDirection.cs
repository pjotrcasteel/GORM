namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Classifies how an outcome responds to an increasing parameter.
/// </summary>
public enum GraphSensitivityDirection
{
    /// <summary>
    /// The sampled outcome does not materially change.
    /// </summary>
    Flat,

    /// <summary>
    /// The sampled outcome never decreases and changes at least once.
    /// </summary>
    Increasing,

    /// <summary>
    /// The sampled outcome never increases and changes at least once.
    /// </summary>
    Decreasing,

    /// <summary>
    /// The sampled outcome changes in more than one direction.
    /// </summary>
    Mixed
}