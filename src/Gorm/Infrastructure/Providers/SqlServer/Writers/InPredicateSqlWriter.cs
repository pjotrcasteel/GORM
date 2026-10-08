using System.Collections;
using System.Linq.Expressions;

namespace Gorm.Infrastructure.Providers.SqlServer.Writers;

/// <summary>
/// Writes SQL IN predicates from supported Contains(...) expressions.
/// </summary>
internal static class InPredicateSqlWriter
{
    public static bool TryWrite(MethodCallExpression expression, Func<Expression, string> writeExpression, Func<object?, string> addParameter, out string sql)
    {
        if (!TryGetContainsArguments(expression, out var collectionExpression, out var itemExpression))
        {
            sql = string.Empty;
            return false;
        }

        var values = ReadEnumerableValues(collectionExpression);
        var itemSql = writeExpression(itemExpression);
        var parameterNames = values is ICollection collection ? new List<string>(collection.Count) : [];
        var seenValues = new HashSet<object?>();
        var containsNull = false;

        foreach (object? value in values)
        {
            if (value is null)
            {
                containsNull = true;
                continue;
            }

            if (seenValues.Add(value))
            {
                parameterNames.Add(addParameter(value));
            }
        }

        sql = BuildInPredicateSql(itemSql, parameterNames, containsNull);
        return true;
    }

    private static string BuildInPredicateSql(string itemSql, List<string> parameterNames, bool containsNull)
    {
        if (parameterNames.Count == 0)
        {
            return containsNull
                ? $"({itemSql} IS NULL)"
                : "(1 = 0)";
        }

        var inSql = $"({itemSql} IN ({string.Join(", ", parameterNames)}))";

        return containsNull
            ? $"({inSql} OR ({itemSql} IS NULL))"
            : inSql;
    }

    private static bool TryGetContainsArguments(MethodCallExpression expression, out Expression collectionExpression, out Expression itemExpression)
    {
        collectionExpression = null!;
        itemExpression = null!;

        if (expression.Method.Name != nameof(Enumerable.Contains))
        {
            return false;
        }

        if (expression.Object is not null &&
            expression.Object.Type != typeof(string) &&
            expression.Arguments.Count == 1)
        {
            collectionExpression = expression.Object;
            itemExpression = expression.Arguments[0];
            return true;
        }

        if (expression.Object is null &&
            PredicateMethodCache.IsSupportedStaticContains(expression.Method.DeclaringType) &&
            expression.Arguments.Count >= 2)
        {
            collectionExpression = expression.Arguments[0];
            itemExpression = expression.Arguments[1];
            return true;
        }

        return false;
    }

    private static IEnumerable ReadEnumerableValues(Expression collectionExpression)
    {
        var normalizedExpression = UnwrapCollectionExpression(collectionExpression);

        return CapturedExpressionValueReader.ReadEnumerable(normalizedExpression, "The IN predicate source must evaluate to an IEnumerable.");
    }

    private static Expression UnwrapCollectionExpression(Expression expression)
    {
        while (expression is MethodCallExpression methodCallExpression)
        {
            if (IsSpanImplicitConversion(methodCallExpression) ||
                IsMemoryExtensionsAsSpan(methodCallExpression))
            {
                expression = methodCallExpression.Arguments[0];
                continue;
            }

            break;
        }

        return expression;
    }

    private static bool IsSpanImplicitConversion(MethodCallExpression expression) =>
        expression.Method.Name == "op_Implicit" &&
        expression.Arguments.Count == 1 &&
        IsSpanType(expression.Method.DeclaringType);

    private static bool IsMemoryExtensionsAsSpan(MethodCallExpression expression) =>
        expression.Method.DeclaringType == typeof(MemoryExtensions) &&
        expression.Method.Name == nameof(MemoryExtensions.AsSpan) &&
        expression.Arguments.Count > 0;

    private static bool IsSpanType(Type? type) =>
        type is not null &&
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>) || type.GetGenericTypeDefinition() == typeof(Span<>));
}