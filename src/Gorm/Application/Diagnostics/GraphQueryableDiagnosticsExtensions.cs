using Gorm.Application.Querying;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph queryable diagnostics extensions.
/// </summary>
public static class GraphQueryableDiagnosticsExtensions
{
    /// <summary>
    /// Explains the query.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="query">The query.</param>
    /// <returns>The result.</returns>
    public static GraphQueryExplainResult Explain<T>(this IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is not GraphQueryProvider provider)
        {
            throw new InvalidOperationException($"The query provider must be '{typeof(GraphQueryProvider).FullName}'.");
        }

        var sql = query.ToSql(provider.Context);
        var debugView = query.ToDebugView();

        return new GraphQueryExplainResult
        {
            Sql = sql.CommandText,
            Parameters = (IReadOnlyList<GraphSqlParameter>)sql.Parameters,
            DebugView = debugView
        };
    }
}