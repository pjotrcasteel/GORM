using System.Globalization;
using System.Linq.Expressions;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;
using Gorm.Infrastructure.Providers.SqlServer.Resolvers;
using Gorm.Infrastructure.Sql;

namespace Gorm.Infrastructure.Providers.SqlServer.Writers;

/// <summary>
/// Represents projected predicate writer.
/// </summary>
public sealed class ProjectedPredicateWriter
{
    private readonly string _alias;
    private readonly ParameterExpression _rootParameter;
    private readonly ProjectedColumnResolver _resolver;
    private readonly IList<GraphSqlParameter> _parameters;
    private readonly Func<string> _nextParameterName;

    public ProjectedPredicateWriter(
        string alias,
        ParameterExpression parameter,
        ProjectedColumnResolver resolver,
        IList<GraphSqlParameter> parameters,
        Func<string> nextParameterName)
    {
        _alias = alias;
        _rootParameter = parameter;
        _resolver = resolver;
        _parameters = parameters;
        _nextParameterName = nextParameterName;
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
        ConstantExpression constant => AddParameter(constant.Value),
        MethodCallExpression methodCall => WriteMethodCall(methodCall),
        UnaryExpression unary => WriteUnary(unary),
        ParameterExpression parameterExpression when parameterExpression == _rootParameter => WriteProjectedParameter(parameterExpression),
        _ => throw new NotSupportedException(
            $"Projected predicate expression '{expression.NodeType}' is not supported yet.")
    };

    private string WriteBinary(BinaryExpression expression)
    {
        if ((expression.NodeType == ExpressionType.Equal || expression.NodeType == ExpressionType.NotEqual) &&
            TryWriteNullComparison(expression, out var nullComparisonSql))
        {
            return nullComparisonSql;
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
        if (expression.Expression == _rootParameter)
        {
            if (!_resolver.TryResolveMember(expression.Member.Name, out var columnName, out _))
            {
                throw new NotSupportedException(
                    $"Projected member '{expression.Member.Name}' is not available in the current Select(...).");
            }

            var columnSql = Column(_alias, columnName);

            return expression.Type == typeof(bool)
                ? $"({columnSql} = CAST(1 AS bit))"
                : columnSql;
        }

        var value = CapturedExpressionValueReader.Read(expression);
        return AddParameter(value);
    }

    private string WriteProjectedParameter(ParameterExpression expression)
    {
        if (_resolver.TryResolveScalar(out var columnName, out _))
        {
            var columnSql = Column(_alias, columnName);

            return expression.Type == typeof(bool)
                ? $"({columnSql} = CAST(1 AS bit))"
                : columnSql;
        }

        throw new NotSupportedException("A projected parameter can only be used directly for scalar Select(...) projections.");
    }

    private string WriteMethodCall(MethodCallExpression expression)
    {
        if (TryWriteProjectedStringMethod(expression, out var stringSql))
        {
            return stringSql;
        }

        if (InPredicateSqlWriter.TryWrite(expression, Write, AddParameter, out var inPredicateSql))
        {
            return inPredicateSql;
        }

        throw new NotSupportedException(
            $"Method call '{expression.Method.DeclaringType?.FullName}.{expression.Method.Name}' is not supported yet.");
    }

    private bool TryWriteProjectedStringMethod(MethodCallExpression expression, out string sql)
    {
        if (TryWriteStaticStringNullCheck(expression, out sql))
        {
            return true;
        }

        if (expression.Object is null ||
            expression.Object.Type != typeof(string) ||
            expression.Arguments.Count != 1 ||
            !TryReadLikeArgument(expression.Arguments[0], out var argumentValue))
        {
            sql = string.Empty;
            return false;
        }

        var instanceSql = Write(expression.Object);
        var escapedValue = SqlGenerationHelpers.EscapeLikePattern(argumentValue);

        sql = expression.Method.Name switch
        {
            nameof(string.StartsWith) => WriteLike(instanceSql, AddParameter(string.Concat(escapedValue, "%"))),
            nameof(string.EndsWith) => WriteLike(instanceSql, AddParameter(string.Concat("%", escapedValue))),
            nameof(string.Contains) => WriteLike(instanceSql, AddParameter(string.Concat("%", escapedValue, "%"))),
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

    private bool TryWriteStaticStringNullCheck(MethodCallExpression expression, out string sql)
    {
        if (expression.Object is not null ||
            expression.Method.DeclaringType != typeof(string) ||
            expression.Arguments.Count != 1)
        {
            sql = string.Empty;
            return false;
        }

        if (expression.Method.Name == nameof(string.IsNullOrWhiteSpace))
        {
            var argumentSql = Write(expression.Arguments[0]);
            var emptySql = AddParameter(string.Empty);

            sql = $"(({argumentSql} IS NULL) OR (LTRIM(RTRIM({argumentSql})) = {emptySql}))";
            return true;
        }

        if (expression.Method.Name == nameof(string.IsNullOrEmpty))
        {
            var argumentSql = Write(expression.Arguments[0]);
            var emptySql = AddParameter(string.Empty);

            sql = $"(({argumentSql} IS NULL) OR ({argumentSql} = {emptySql}))";
            return true;
        }

        sql = string.Empty;
        return false;
    }

    private string WriteUnary(UnaryExpression expression) => expression.NodeType switch
    {
        ExpressionType.Convert => Write(expression.Operand),
        ExpressionType.ConvertChecked => Write(expression.Operand),
        ExpressionType.Not => $"(NOT {Write(expression.Operand)})",
        _ => throw new NotSupportedException(
            $"Unary operator '{expression.NodeType}' is not supported yet.")
    };

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

    private string AddParameter(object? value)
    {
        var name = _nextParameterName();

        _parameters.Add(GraphSqlParameter.Create(name, value));

        return name;
    }

    private static string WriteLike(string instanceSql, string parameterName) =>
        $"({instanceSql} LIKE {parameterName} {SqlGenerationHelpers.LikeEscapeSql})";

    private static string Column(string tableAlias, string columnName) =>
        SqlGenerationHelpers.EscapeColumn(tableAlias, columnName);

    private static bool IsNullConstant(Expression expression) =>
        expression is ConstantExpression constantExpression && constantExpression.Value is null;
}