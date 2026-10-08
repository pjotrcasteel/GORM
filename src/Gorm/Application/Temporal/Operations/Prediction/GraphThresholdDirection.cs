namespace Gorm.Application.Temporal.Operations.Prediction;

/// <summary>
/// Defines which side of a threshold represents a breach.
/// </summary>
public enum GraphThresholdDirection
{
    /// <summary>
    /// Values at or above the threshold are breached.
    /// </summary>
    AtOrAbove,

    /// <summary>
    /// Values at or below the threshold are breached.
    /// </summary>
    AtOrBelow
}