namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Classifies the materiality of expected-versus-observed graph drift.
/// </summary>
public enum GraphTwinDriftSeverity
{
    /// <summary>
    /// No entity state differs.
    /// </summary>
    None,

    /// <summary>
    /// A small property-only difference exists.
    /// </summary>
    Low,

    /// <summary>
    /// Topology changed or the configured high ratio was reached.
    /// </summary>
    High,

    /// <summary>
    /// The configured critical change ratio was reached.
    /// </summary>
    Critical
}