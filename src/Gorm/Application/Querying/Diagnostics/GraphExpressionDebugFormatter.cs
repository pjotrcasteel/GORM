using System.Linq.Expressions;

namespace Gorm.Application.Querying.Diagnostics;

/// <summary>
/// Represents graph expression debug formatter.
/// </summary>
internal static class GraphExpressionDebugFormatter
{
    /// <summary>
    /// Executes format.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public static string Format(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var writer = new Writer(expression.Parameters);
        return writer.Write(expression);
    }

    /// <summary>
    /// Represents writer.
    /// </summary>
    private sealed class Writer
    {
        private readonly IReadOnlyList<ParameterExpression> _parameters;

        /// <summary>
        /// Executes writer.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        public Writer(IReadOnlyList<ParameterExpression> parameters)
        {
            _parameters = parameters;
        }

        /// <summary>
        /// Executes write.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The value.</returns>
        public string Write(Expression expression) => expression switch
        {
            LambdaExpression lambda => WriteLambda(lambda),
            BinaryExpression binary => WriteBinary(binary),
            MemberExpression member => WriteMember(member),
            ConstantExpression constant => WriteConstant(constant.Value),
            MethodCallExpression methodCall => WriteMethodCall(methodCall),
            UnaryExpression unary => WriteUnary(unary),
            ParameterExpression parameter => parameter.Name ?? parameter.Type.Name,
            _ => expression.ToString()
        };

        private string WriteLambda(LambdaExpression expression)
        {
            var parameters = string.Join(", ", expression.Parameters.Select(x => x.Name ?? x.Type.Name));
            return $"{parameters} => {Write(expression.Body)}";
        }

        private string WriteBinary(BinaryExpression expression)
        {
            var op = expression.NodeType switch
            {
                ExpressionType.Equal => "==",
                ExpressionType.NotEqual => "!=",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.AndAlso => "&&",
                ExpressionType.OrElse => "||",
                _ => expression.NodeType.ToString()
            };

            return $"({Write(expression.Left)} {op} {Write(expression.Right)})";
        }

        private string WriteMethodCall(MethodCallExpression expression)
        {
            var target = expression.Object is null ? null : Write(expression.Object);
            var args = string.Join(", ", expression.Arguments.Select(Write));

            if (target is not null)
            {
                return $"{target}.{expression.Method.Name}({args})";
            }

            return $"{expression.Method.Name}({args})";
        }

        private string WriteUnary(UnaryExpression expression)
        {
            if (expression.NodeType == ExpressionType.Convert)
            {
                return Write(expression.Operand);
            }

            if (expression.NodeType == ExpressionType.Not)
            {
                return $"!{Write(expression.Operand)}";
            }

            return expression.ToString();
        }

        private string WriteMember(MemberExpression expression)
        {
            if (expression.Expression is ParameterExpression parameter &&
                _parameters.Contains(parameter))
            {
                return $"{parameter.Name}.{expression.Member.Name}";
            }

            var value = TryEvaluate(expression);
            if (value.success)
            {
                return WriteConstant(value.value);
            }

            if (expression.Expression is not null)
            {
                return $"{Write(expression.Expression)}.{expression.Member.Name}";
            }

            return expression.Member.Name;
        }

        private static (bool success, object? value) TryEvaluate(Expression expression)
        {
            try
            {
                var converted = Expression.Convert(expression, typeof(object));
                var lambda = Expression.Lambda<Func<object?>>(converted);
                var compiled = lambda.Compile();
                return (true, compiled());
            }
            catch
            {
                return (false, null);
            }
        }

        private static string WriteConstant(object? value) => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty
        };
    }
}