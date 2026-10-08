using System.Linq.Expressions;

namespace Gorm.Core.Loading;

/// <summary>
/// Represents graph include request.
/// </summary>
public sealed class GraphIncludeRequest
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the name kind.
    /// </summary>
    public required GraphIncludeNameKind NameKind { get; init; }

    /// <summary>
    /// Gets or sets the include edge.
    /// </summary>
    public bool IncludeEdge { get; init; }

    /// <summary>
    /// Gets or sets the edge predicate.
    /// </summary>
    public LambdaExpression? EdgePredicate { get; init; }

    /// <summary>
    /// Gets or sets the related predicate.
    /// </summary>
    public LambdaExpression? RelatedPredicate { get; init; }

    /// <summary>
    /// Gets or sets the edge order by.
    /// </summary>
    public LambdaExpression? EdgeOrderBy { get; init; }

    /// <summary>
    /// Gets or sets the edge order descending.
    /// </summary>
    public bool EdgeOrderDescending { get; init; }

    /// <summary>
    /// Gets or sets the related order by.
    /// </summary>
    public LambdaExpression? RelatedOrderBy { get; init; }

    /// <summary>
    /// Gets or sets the related order descending.
    /// </summary>
    public bool RelatedOrderDescending { get; init; }

    /// <summary>
    /// Gets or sets the skip.
    /// </summary>
    public int? Skip { get; init; }

    /// <summary>
    /// Gets or sets the take.
    /// </summary>
    public int? Take { get; init; }

    /// <summary>
    ///
    /// </summary>
    private readonly List<GraphIncludeRequest> _children = [];

    /// <summary>
    /// Gets or sets the children.
    /// </summary>
    public IReadOnlyList<GraphIncludeRequest> Children => _children;

    public void AddChild(GraphIncludeRequest child)
    {
        ArgumentNullException.ThrowIfNull(child);
        _children.Add(child);
    }

    public void AddChildren(IReadOnlyList<GraphIncludeRequest> children)
    {
        _children.AddRange(children);
    }
}