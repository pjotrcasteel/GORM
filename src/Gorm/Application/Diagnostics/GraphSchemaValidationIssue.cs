namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph schema validation issue.
/// </summary>
public sealed class GraphSchemaValidationIssue
{
    /// <summary>
    /// Gets the issue code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the issue severity.
    /// </summary>
    public GraphSchemaValidationSeverity Severity { get; init; } = GraphSchemaValidationSeverity.Error;

    /// <summary>
    /// Gets the human-readable description.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the expected value, if applicable.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// Gets the actual value, if applicable.
    /// </summary>
    public string? Actual { get; init; }

    /// <summary>
    /// Gets the database schema name, if applicable.
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// Gets the table name, if applicable.
    /// </summary>
    public string? Table { get; init; }

    /// <summary>
    /// Gets the column name, if applicable.
    /// </summary>
    public string? Column { get; init; }

    /// <summary>
    /// Gets the index name, if applicable.
    /// </summary>
    public string? Index { get; init; }
}