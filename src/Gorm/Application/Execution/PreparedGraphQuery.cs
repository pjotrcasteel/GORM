using Gorm.Core.Loading;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents prepared graph query.
/// </summary>
/// <typeparam name="T">The query item type.</typeparam>
internal sealed class PreparedGraphQuery<T>
{
    /// <summary>
    /// Gets or sets the query.
    /// </summary>
    public required IQueryable<T> Query { get; init; }

    /// <summary>
    /// Gets or sets the includes.
    /// </summary>
    public required IReadOnlyList<GraphIncludeRequest> Includes { get; init; }
}