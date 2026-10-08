using System.Collections;
using System.Linq.Expressions;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph includable queryable.
/// </summary>
internal sealed class GraphIncludableQueryable<TEntity, TProperty> : IGraphIncludableQueryable<TEntity, TProperty>, IOrderedQueryable<TEntity>
{
    public GraphIncludableQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public Type ElementType => typeof(TEntity);

    /// <summary>
    /// Gets or sets the expression.
    /// </summary>
    public Expression Expression { get; }

    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public IQueryProvider Provider { get; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <returns>The value.</returns>
    public IEnumerator<TEntity> GetEnumerator() => Provider.Execute<IEnumerable<TEntity>>(Expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Executes to string.
    /// </summary>
    /// <returns>The value.</returns>
    public override string ToString() => Expression.ToString() ?? base.ToString() ?? typeof(TEntity).Name;
}