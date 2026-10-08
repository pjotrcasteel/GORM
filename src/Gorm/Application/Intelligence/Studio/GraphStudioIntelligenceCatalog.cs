namespace Gorm.Application.Intelligence.Studio;

/// <summary>
/// Represents one immutable discovery snapshot for a desktop GormStudio client.
/// </summary>
public sealed class GraphStudioIntelligenceCatalog
{
    /// <summary>
    /// Gets the metadata schema version.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// Gets the UTC catalog generation instant.
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Gets identifier-ordered algorithms.
    /// </summary>
    public required IReadOnlyList<GraphStudioAlgorithmMetadata> Algorithms { get; init; }
}