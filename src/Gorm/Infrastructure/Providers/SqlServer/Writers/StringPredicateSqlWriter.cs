using System.Globalization;
using System.Linq.Expressions;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Infrastructure.Providers.SqlServer.Writers;

/// <summary>
/// Writes SQL predicates for supported string expressions.
/// </summary>
internal static class StringPredicateSqlWriter
{
    public static bool TryWrite(MethodCallExpression expression, Func<Expression, string> writeExpression, Func<object?, string> addParameter, out string sql)
    {
        if (TryWriteStringNullCheck(expression, writeExpression, addParameter, out sql))
        {
            return true;
        }

        if (expression.Object is not null && expression.Object.Type == typeof(string))
        {
            return TryWriteStringLike(expression, writeExpression, addParameter, out sql);
        }

        sql = string.Empty;
        return false;
    }

    private static bool TryWriteStringNullCheck(MethodCallExpression expression, Func<Expression, string> writeExpression, Func<object?, string> addParameter, out string sql)
    {
        if (IsStaticStringMethod(expression, nameof(string.IsNullOrWhiteSpace)))
        {
            var argumentSql = writeExpression(expression.Arguments[0]);
            var emptySql = addParameter(string.Empty);

            sql = $"(({argumentSql} IS NULL) OR (LTRIM(RTRIM({argumentSql})) = {emptySql}))";
            return true;
        }

        if (IsStaticStringMethod(expression, nameof(string.IsNullOrEmpty)))
        {
            var argumentSql = writeExpression(expression.Arguments[0]);
            var emptySql = addParameter(string.Empty);

            sql = $"(({argumentSql} IS NULL) OR ({argumentSql} = {emptySql}))";
            return true;
        }

        sql = string.Empty;
        return false;
    }

    private static bool TryWriteStringLike(MethodCallExpression expression, Func<Expression, string> writeExpression, Func<object?, string> addParameter, out string sql)
    {
        if (expression.Arguments.Count != 1 ||
            expression.Object is null ||
            !TryReadLikeArgument(expression.Arguments[0], out var argumentValue))
        {
            sql = string.Empty;
            return false;
        }

        var instanceSql = writeExpression(expression.Object);
        var escapedArgumentValue = SqlGenerationHelpers.EscapeLikePattern(argumentValue);

        sql = expression.Method.Name switch
        {
            nameof(string.StartsWith) => WriteLike(instanceSql, addParameter(string.Concat(escapedArgumentValue, "%"))),
            nameof(string.EndsWith) => WriteLike(instanceSql, addParameter(string.Concat("%", escapedArgumentValue))),
            nameof(string.Contains) => WriteLike(instanceSql, addParameter(string.Concat("%", escapedArgumentValue, "%"))),
            _ => string.Empty
        };

        return sql.Length > 0;
    }

    private static bool TryReadLikeArgument(Expression expression, out string value)
    {
        if (expression.Type != typeof(string) && expression.Type != typeof(char))
        {
            value = string.Empty;
            return false;
        }

        var rawValue = CapturedExpressionValueReader.Read(expression);

        value = rawValue switch
        {
            null => string.Empty,
            char charValue => charValue.ToString(CultureInfo.InvariantCulture),
            string stringValue => stringValue,
            _ => rawValue.ToString() ?? string.Empty
        };

        return true;
    }

    private static bool IsStaticStringMethod(MethodCallExpression expression, string methodName) =>
        expression.Object is null &&
        expression.Method.DeclaringType == typeof(string) &&
        expression.Method.Name == methodName &&
        expression.Arguments.Count == 1;

    private static string WriteLike(string instanceSql, string parameterName) =>
        $"({instanceSql} LIKE {parameterName} {SqlGenerationHelpers.LikeEscapeSql})";
}