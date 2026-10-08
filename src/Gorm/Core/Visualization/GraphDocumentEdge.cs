namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph document edge.
/// </summary>
public sealed class GraphDocumentEdge
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the from.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Gets or sets the to.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets or sets the Properties.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
}