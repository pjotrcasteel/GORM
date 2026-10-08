using System.Linq.Expressions;
using Gorm.Core.Primitives;

namespace Gorm.Core.Loading;

/// <summary>
/// Represents graph relationship include builder.
/// </summary>
public sealed class GraphRelationshipIncludeBuilder<TRelated>
    where TRelated : Node
{
    private readonly string _name;
    private readonly GraphIncludeNameKind _nameKind;
    private readonly bool _includeEdge;

    private LambdaExpression? _edgePredicate;
    private LambdaExpression? _relatedPredicate;
    private LambdaExpression? _edgeOrderBy;
    private bool _edgeOrderDescending;
    private LambdaExpression? _relatedOrderBy;
    private bool _relatedOrderDescending;
    private int? _skip;
    private int? _take;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphRelationshipIncludeBuilder{TRelated}"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="nameKind">The name kind.</param>
    /// <param name="includeEdge">The include edge.</param>
    public GraphRelationshipIncludeBuilder(string name, GraphIncludeNameKind nameKind, bool includeEdge)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Include name cannot be null or whitespace.", nameof(name));
        }

        _name = name;
        _nameKind = nameKind;
        _includeEdge = includeEdge;
    }

    /// <summary>
    /// Executes where.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> Where(Expression<Func<TRelated, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _relatedPredicate = predicate;
        return this;
    }

    /// <summary>
    /// Executes where edge.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> WhereEdge<TEdge>(Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _edgePredicate = predicate;
        return this;
    }

    /// <summary>
    /// Executes order by.
    /// </summary>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> OrderBy<TKey>(Expression<Func<TRelated, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _relatedOrderBy = keySelector;
        _relatedOrderDescending = false;
        return this;
    }

    /// <summary>
    /// Executes order by descending.
    /// </summary>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> OrderByDescending<TKey>(Expression<Func<TRelated, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _relatedOrderBy = keySelector;
        _relatedOrderDescending = true;
        return this;
    }

    /// <summary>
    /// Executes order by edge.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> OrderByEdge<TEdge, TKey>(Expression<Func<TEdge, TKey>> keySelector)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _edgeOrderBy = keySelector;
        _edgeOrderDescending = false;
        return this;
    }

    /// <summary>
    /// Executes order by edge descending.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TKey">The type of t key.</typeparam>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> OrderByEdgeDescending<TEdge, TKey>(Expression<Func<TEdge, TKey>> keySelector)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _edgeOrderBy = keySelector;
        _edgeOrderDescending = true;
        return this;
    }

    /// <summary>
    /// Executes skip.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> Skip(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Skip count cannot be negative.");
        }

        _skip = count;
        return this;
    }

    /// <summary>
    /// Executes take.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>The builder.</returns>
    public GraphRelationshipIncludeBuilder<TRelated> Take(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Take count cannot be negative.");
        }

        _take = count;
        return this;
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphIncludeRequest Build() => new()
    {
        Name = _name,
        NameKind = _nameKind,
        IncludeEdge = _includeEdge,
        EdgePredicate = _edgePredicate,
        RelatedPredicate = _relatedPredicate,
        EdgeOrderBy = _edgeOrderBy,
        EdgeOrderDescending = _edgeOrderDescending,
        RelatedOrderBy = _relatedOrderBy,
        RelatedOrderDescending = _relatedOrderDescending,
        Skip = _skip,
        Take = _take
    };
}