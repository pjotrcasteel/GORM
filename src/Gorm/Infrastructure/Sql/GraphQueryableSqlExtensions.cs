using Gorm.Application.Context;
using Gorm.Application.Querying;

namespace Gorm.Infrastructure.Sql;

/// <summary>
/// Represents graph queryable sql extensions.
/// </summary>
public static class GraphQueryableSqlExtensions
{
    /// <summary>
    /// Executes to sql.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="context">The graph context.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery ToSql<T>(this IQueryable<T> query, GraphContext context)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        if (query.Provider is not GraphQueryProvider _)
        {
            throw new InvalidOperationException(
                $"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        var queryModel = GraphQueryProvider.Translate(query.Expression);
        return context.Provider.QuerySqlGenerator.Generate(context.Model, queryModel);
    }
}