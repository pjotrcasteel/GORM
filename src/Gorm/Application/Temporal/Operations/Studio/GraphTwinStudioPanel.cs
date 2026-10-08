namespace Gorm.Application.Temporal.Operations.Studio;

/// <summary>
/// Describes one report panel for a native GormStudio shell.
/// </summary>
public sealed class GraphTwinStudioPanel
{
    /// <summary>
    /// Gets stable panel id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets display title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets evidence item count.
    /// </summary>
    public required int ItemCount { get; init; }
}