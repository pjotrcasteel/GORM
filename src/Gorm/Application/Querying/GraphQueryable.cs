using System.Collections;
using System.Linq.Expressions;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph queryable.
/// </summary>
public sealed class GraphQueryable<T> : IOrderedQueryable<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryable{T}"/> class.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="expression">The expression.</param>
    public GraphQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <summary>
    ///
    /// </summary>
    public Type ElementType => typeof(T);

    /// <inheritdoc/>
    public Expression Expression { get; }

    /// <inheritdoc/>
    public IQueryProvider Provider { get; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <returns>The value.</returns>
    public IEnumerator<T> GetEnumerator() => Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Executes to string.
    /// </summary>
    /// <returns>The value.</returns>
    public override string ToString() => Expression.ToString() ?? base.ToString() ?? typeof(T).Name;
}