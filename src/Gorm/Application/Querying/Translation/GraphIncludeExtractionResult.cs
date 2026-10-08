using System.Linq.Expressions;
using Gorm.Core.Loading;

namespace Gorm.Application.Querying.Translation;
/// <summary>
/// Represents graph include extraction result.
/// </summary>
internal sealed class GraphIncludeExtractionResult
{
    /// <summary>
    /// Gets or sets the query expression.
    /// </summary>
    public required Expression QueryExpression { get; init; }

    /// <summary>
    /// Gets or sets the includes.
    /// </summary>
    public required IReadOnlyList<GraphIncludeRequest> Includes { get; init; }
}