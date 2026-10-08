using System.Collections;
using System.Linq.Expressions;
using Gorm.Application.Context;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;

namespace Gorm.Core.Sets;

/// <summary>
/// Represents graph set base.
/// </summary>
public abstract class GraphSetBase<TEntity, TMapping> : IOrderedQueryable<TEntity>
    where TEntity : class
    where TMapping : class
{
    private readonly GraphQueryable<TEntity> _queryable;
    private readonly GraphQueryProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphSetBase{TEntity, TMapping}"/> class.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="mapping">The mapping.</param>
    /// <param name="elementKind">The element kind.</param>
    protected GraphSetBase(GraphContext context, TMapping mapping, GraphQueryElementKind elementKind)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        _provider = new GraphQueryProvider(context);
        var expression = new GraphQueryRootExpression(typeof(TEntity), elementKind);
        _queryable = new GraphQueryable<TEntity>(_provider, expression);
    }

    /// <summary>
    /// Gets the owning graph context.
    /// </summary>
    public GraphContext Context { get; }

    /// <summary>
    /// Gets the mapping metadata for the set.
    /// </summary>
    public TMapping Mapping { get; }

    /// <summary>
    /// Gets the type of TEntity.
    /// </summary>
    public Type ElementType => typeof(TEntity);

    /// <summary>
    /// Gets expression.
    /// </summary>
    /// <returns>The value.</returns>
    public Expression Expression => _queryable.Expression;

    /// <summary>
    /// Gets provider.
    /// </summary>
    /// <returns>The value.</returns>
    public IQueryProvider Provider => _provider;

    /// <summary>
    /// Executes to query model.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphQueryModel ToQueryModel() => GraphQueryProvider.Translate(Expression);

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <returns>The value.</returns>
    public IEnumerator<TEntity> GetEnumerator() => _queryable.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Executes to string.
    /// </summary>
    /// <returns>The value.</returns>
    public override string ToString() => _queryable.ToString();
}