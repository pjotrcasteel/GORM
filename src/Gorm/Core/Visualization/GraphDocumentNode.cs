namespace Gorm.Core.Visualization;

/// <summary>
/// Represents graph document node.
/// </summary>
public sealed class GraphDocumentNode
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the depth.
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// Gets a value indicating whether is root.
    /// </summary>
    public bool IsRoot { get; init; }

    /// <summary>
    /// Gets or sets the Properties.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
}