using System.Linq.Expressions;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Infrastructure.Providers.SqlServer.Writers;

/// <summary>
/// Represents predicate writer.
/// </summary>
internal sealed class PredicateWriter
{
    private readonly SqlGenerationContext _context;
    private readonly ParameterExpression _parameter;
    private readonly string _alias;

    public PredicateWriter(SqlGenerationContext context, ParameterExpression parameter, string alias)
    {
        _context = context;
        _parameter = parameter;
        _alias = alias;
    }

    /// <summary>
    /// Executes write.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public string Write(Expression expression) => expression switch
    {
        BinaryExpression binary => WriteBinary(binary),
        MemberExpression member => WriteMemberOrCapturedValue(member),
        ConstantExpression constant => WriteValueAsParameter(constant.Value),
        MethodCallExpression methodCall => WriteMethodCall(methodCall),
        UnaryExpression unary => WriteUnary(unary),
        ParameterExpression parameterExpression when parameterExpression == _parameter && parameterExpression.Type == typeof(bool) =>
            $"({Column(_alias, "Value")} = CAST(1 AS bit))",
        ParameterExpression parameterExpression when parameterExpression == _parameter =>
            Column(_alias, "Value"),
        _ => throw new NotSupportedException(
            $"Predicate expression '{expression.NodeType}' is not supported yet.")
    };

    private string WriteBinary(BinaryExpression expression)
    {
        if ((expression.NodeType == ExpressionType.Equal || expression.NodeType == ExpressionType.NotEqual) &&
            TryWriteNullComparison(expression, out var nullComparisonSql))
        {
            return nullComparisonSql;
        }

        // SQL Server CONCAT preserves C# string-concatenation null semantics while retaining parameterization.
        if (expression.NodeType == ExpressionType.Add && expression.Type == typeof(string))
        {
            return $"CONCAT({Write(expression.Left)}, {Write(expression.Right)})";
        }

        var left = Write(expression.Left);
        var right = Write(expression.Right);

        var op = expression.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            _ => throw new NotSupportedException(
                $"Binary operator '{expression.NodeType}' is not supported yet.")
        };

        return string.Concat("(", left, " ", op, " ", right, ")");
    }

    private string WriteMemberOrCapturedValue(MemberExpression expression)
    {
        if (expression.Expression == _parameter)
        {
            var columnSql = Column(_alias, expression.Member.Name);

            return expression.Type == typeof(bool)
                ? $"({columnSql} = CAST(1 AS bit))"
                : columnSql;
        }

        var value = CapturedExpressionValueReader.Read(expression);
        return WriteValueAsParameter(value);
    }

    private string WriteMethodCall(MethodCallExpression expression)
    {
        if (InPredicateSqlWriter.TryWrite(expression, Write, WriteValueAsParameter, out var inPredicateSql))
        {
            return inPredicateSql;
        }

        if (StringPredicateSqlWriter.TryWrite(expression, Write, WriteValueAsParameter, out var stringPredicateSql))
        {
            return stringPredicateSql;
        }

        throw new NotSupportedException(
            $"Method call '{expression.Method.DeclaringType?.FullName}.{expression.Method.Name}' is not supported yet.");
    }

    private string WriteUnary(UnaryExpression expression) => expression.NodeType switch
    {
        ExpressionType.Convert => Write(expression.Operand),
        ExpressionType.ConvertChecked => Write(expression.Operand),
        ExpressionType.Not => WriteNot(expression),
        _ => throw new NotSupportedException(
            $"Unary operator '{expression.NodeType}' is not supported yet.")
    };

    private string WriteNot(UnaryExpression expression)
    {
        if (expression.Operand is MemberExpression memberExpression &&
            memberExpression.Expression == _parameter &&
            memberExpression.Type == typeof(bool))
        {
            return $"({Column(_alias, memberExpression.Member.Name)} = CAST(0 AS bit))";
        }

        return $"(NOT {Write(expression.Operand)})";
    }

    private bool TryWriteNullComparison(BinaryExpression expression, out string sql)
    {
        if (IsNullConstant(expression.Left))
        {
            var right = Write(expression.Right);
            sql = expression.NodeType == ExpressionType.Equal
                ? $"({right} IS NULL)"
                : $"({right} IS NOT NULL)";
            return true;
        }

        if (IsNullConstant(expression.Right))
        {
            var left = Write(expression.Left);
            sql = expression.NodeType == ExpressionType.Equal
                ? $"({left} IS NULL)"
                : $"({left} IS NOT NULL)";
            return true;
        }

        sql = string.Empty;
        return false;
    }

    private string WriteValueAsParameter(object? value) => _context.AddParameter(value);

    private static string Column(string tableAlias, string columnName) => SqlGenerationHelpers.EscapeColumn(tableAlias, columnName);

    private static bool IsNullConstant(Expression expression) =>
        expression is ConstantExpression constantExpression && constantExpression.Value is null;
}