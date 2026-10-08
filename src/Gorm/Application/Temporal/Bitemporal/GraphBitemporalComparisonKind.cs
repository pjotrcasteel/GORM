namespace Gorm.Application.Temporal.Bitemporal;

/// <summary>
/// Classifies which temporal axis changed in a world comparison.
/// </summary>
public enum GraphBitemporalComparisonKind
{
    /// <summary>
    /// Neither temporal axis changed.
    /// </summary>
    SameCoordinate,

    /// <summary>
    /// Business-valid time changed while the knowledge cutoff stayed fixed.
    /// </summary>
    ValidTimeEvolution,

    /// <summary>
    /// Recorded knowledge changed while the represented business instant stayed fixed.
    /// </summary>
    RecordedKnowledgeCorrection,

    /// <summary>
    /// Both temporal axes changed.
    /// </summary>
    Mixed
}