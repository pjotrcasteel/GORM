namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Classifies application-owned early warnings.
/// </summary>
public enum GraphTwinWarningSeverity
{
    /// <summary>
    /// Informational signal.
    /// </summary>
    Information,

    /// <summary>
    /// Action may soon be required.
    /// </summary>
    Warning,

    /// <summary>
    /// Immediate attention is required.
    /// </summary>
    Critical
}