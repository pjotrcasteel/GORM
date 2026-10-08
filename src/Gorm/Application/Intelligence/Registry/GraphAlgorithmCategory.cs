namespace Gorm.Application.Intelligence.Registry;

/// <summary>
/// Groups discoverable graph algorithms by user intent.
/// </summary>
public enum GraphAlgorithmCategory
{
    /// <summary>
    /// Importance and centrality scoring.
    /// </summary>
    Centrality,

    /// <summary>
    /// Topology and structural analysis.
    /// </summary>
    Structure,

    /// <summary>
    /// Community detection and clustering.
    /// </summary>
    Community,

    /// <summary>
    /// Missing relationship prediction.
    /// </summary>
    Prediction,

    /// <summary>
    /// Routing and path optimization.
    /// </summary>
    Routing,

    /// <summary>
    /// Capacity, flow and cut analysis.
    /// </summary>
    Flow,

    /// <summary>
    /// Planning and scheduling.
    /// </summary>
    Planning,

    /// <summary>
    /// Application-defined algorithms.
    /// </summary>
    Custom
}