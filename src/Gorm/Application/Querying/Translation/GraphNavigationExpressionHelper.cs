using System.Linq.Expressions;

namespace Gorm.Application.Querying.Translation;

/// <summary>
/// Represents graph navigation expression helper.
/// </summary>
internal static class GraphNavigationExpressionHelper
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public static string GetPropertyName(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression unaryExpression &&
            unaryExpression.Operand is MemberExpression unaryMemberExpression)
        {
            return unaryMemberExpression.Member.Name;
        }

        throw new InvalidOperationException(nameof(expression));
    }
}