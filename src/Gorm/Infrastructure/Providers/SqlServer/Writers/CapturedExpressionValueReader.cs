using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Gorm.Infrastructure.Providers.SqlServer.Writers;

/// <summary>
/// Reads constant and captured expression values without compiling query expression trees.
/// </summary>
internal static class CapturedExpressionValueReader
{
    private static readonly ConcurrentDictionary<MemberInfo, Func<object?, object?>> MemberReaders = [];

    /// <summary>
    /// Executes read.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public static object? Read(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            ConstantExpression constantExpression => constantExpression.Value,
            MemberExpression memberExpression => ReadMember(memberExpression),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked => Read(unaryExpression.Operand),
            NewArrayExpression newArrayExpression => ReadArray(newArrayExpression),
            MethodCallExpression methodCallExpression => ReadMethodCall(methodCallExpression),
            _ => throw new NotSupportedException($"Expression '{expression.NodeType}' cannot be evaluated as a captured query value.")
        };
    }

    /// <summary>
    /// Executes read enumerable.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The value.</returns>
    public static IEnumerable ReadEnumerable(Expression expression, string message)
    {
        var value = Read(expression);

        return value as IEnumerable ?? throw new NotSupportedException(message);
    }

    private static object? ReadMember(MemberExpression expression)
    {
        var instance = expression.Expression is null ? null : Read(expression.Expression);
        var reader = MemberReaders.GetOrAdd(expression.Member, CreateMemberReader);

        return reader(instance);
    }

    private static Func<object?, object?> CreateMemberReader(MemberInfo member)
    {
        return member switch
        {
            FieldInfo fieldInfo => CreateFieldReader(fieldInfo),
            PropertyInfo propertyInfo => CreatePropertyReader(propertyInfo),
            _ => throw new NotSupportedException($"Member '{member.Name}' cannot be evaluated as a captured query value.")
        };
    }

    private static Func<object?, object?> CreateFieldReader(FieldInfo field)
    {
        var instance = Expression.Parameter(typeof(object), "instance");

        var access = field.IsStatic ? Expression.Field(null, field) : Expression.Field(Expression.Convert(instance, field.DeclaringType!), field);

        return Expression.Lambda<Func<object?, object?>>(Expression.Convert(access, typeof(object)), instance).Compile();
    }

    private static Func<object?, object?> CreatePropertyReader(PropertyInfo property)
    {
        var getter = property.GetGetMethod(nonPublic: true) ?? throw new NotSupportedException(
            $"Property '{property.Name}' cannot be evaluated as a captured query value because it has no getter.");

        var instance = Expression.Parameter(typeof(object), "instance");

        var access = getter.IsStatic ? Expression.Property(null, property) : Expression.Property(Expression.Convert(instance, property.DeclaringType!), property);

        return Expression.Lambda<Func<object?, object?>>(Expression.Convert(access, typeof(object)), instance).Compile();
    }

    private static Array ReadArray(NewArrayExpression expression)
    {
        var elementType = expression.Type.GetElementType() ?? typeof(object);
        var array = Array.CreateInstance(elementType, expression.Expressions.Count);

        for (var i = 0; i < expression.Expressions.Count; i++)
        {
            array.SetValue(Read(expression.Expressions[i]), i);
        }

        return array;
    }

    private static object? ReadMethodCall(MethodCallExpression expression)
    {
        if (TryReadSpanImplicitConversion(expression, out var spanValue))
        {
            return spanValue;
        }

        throw new NotSupportedException($"Method call '{expression.Method.DeclaringType?.FullName}.{expression.Method.Name}' " +
            "cannot be evaluated as a captured query value. Evaluate the value before building the query.");
    }

    private static bool TryReadSpanImplicitConversion(MethodCallExpression expression, out object? value)
    {
        if (expression.Method.Name == "op_Implicit" &&
            expression.Arguments.Count == 1 &&
            IsSpanType(expression.Method.DeclaringType))
        {
            value = Read(expression.Arguments[0]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsSpanType(Type? type) =>
        type is not null &&
        type.IsGenericType &&
        (type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>) || type.GetGenericTypeDefinition() == typeof(Span<>));
}