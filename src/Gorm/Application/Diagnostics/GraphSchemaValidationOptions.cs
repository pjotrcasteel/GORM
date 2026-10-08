namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents database-first schema validation options.
/// </summary>
public sealed class GraphSchemaValidationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether mapped column store types are compared with database metadata.
    /// </summary>
    public bool ValidateColumnTypes { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether mapped column nullability is compared with database metadata.
    /// </summary>
    public bool ValidateColumnNullability { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether mapped indexes are compared with database metadata.
    /// </summary>
    public bool ValidateIndexes { get; init; } = true;
}