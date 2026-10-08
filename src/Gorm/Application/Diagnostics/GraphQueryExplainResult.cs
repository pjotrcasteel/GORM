using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph query explain result.
/// </summary>
public sealed class GraphQueryExplainResult
{
    /// <summary>
    /// Gets the generated SQL text.
    /// </summary>
    public required string Sql { get; init; }

    /// <summary>
    /// Gets the generated SQL parameters.
    /// </summary>
    public required IReadOnlyList<GraphSqlParameter> Parameters { get; init; }

    /// <summary>
    /// Gets the query debug view.
    /// </summary>
    public required string DebugView { get; init; }
}