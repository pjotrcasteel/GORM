namespace Gorm.Application.Temporal.Operations.Studio;

/// <summary>
/// Provides a compact report explorer model for the native desktop application.
/// </summary>
public sealed class GraphTwinStudioExplorerModel
{
    /// <summary>
    /// Gets report id.
    /// </summary>
    public required string ReportId { get; init; }

    /// <summary>
    /// Gets integrity fingerprint.
    /// </summary>
    public required string ReportFingerprint { get; init; }

    /// <summary>
    /// Gets report headline.
    /// </summary>
    public required string Headline { get; init; }

    /// <summary>
    /// Gets fixed panel descriptors.
    /// </summary>
    public required IReadOnlyList<GraphTwinStudioPanel> Panels { get; init; }
}