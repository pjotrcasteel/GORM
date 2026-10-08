using Gorm.Application.Querying.Models;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph queryable debug extensions.
/// </summary>
public static class GraphQueryableDebugExtensions
{
    /// <summary>
    /// Executes to query model.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static GraphQueryModel ToQueryModel<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is not GraphQueryProvider _)
        {
            throw new InvalidOperationException(
                $"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        return GraphQueryProvider.Translate(query.Expression);
    }

    /// <summary>
    /// Executes to debug view.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static string ToDebugView<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var model = query.ToQueryModel();
        return GraphQueryDebugView.Format(model);
    }

    /// <summary>
    /// Executes to sql.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery ToSql<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is not GraphQueryProvider provider)
        {
            throw new InvalidOperationException(
                $"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        var model = GraphQueryProvider.Translate(query.Expression);
        return provider.Context.Provider.QuerySqlGenerator.Generate(provider.Context.Model, model);
    }
}