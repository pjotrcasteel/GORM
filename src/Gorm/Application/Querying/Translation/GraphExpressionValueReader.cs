using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Gorm.Application.Querying.Translation;

/// <summary>
/// Reads constant and captured expression values without compiling expression trees per read.
/// </summary>
internal static class GraphExpressionValueReader
{
    private static readonly ConcurrentDictionary<MemberInfo, Func<object?, object?>> MemberReaders = [];
    private static readonly ConcurrentDictionary<Type, Array> EmptyArrays = [];

    public static object? Read(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            ConstantExpression constant => constant.Value,
            MemberExpression member => ReadMember(member),
            UnaryExpression unary when unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked => Read(unary.Operand),
            NewArrayExpression array => ReadArray(array),
            MethodCallExpression methodCall => ReadKnownMethodCall(methodCall),
            _ => throw new NotSupportedException($"Expression '{expression.NodeType}' cannot be evaluated as a captured query value.")
        };
    }

    public static int ReadInt(Expression expression, string methodName)
    {
        var value = Read(expression);

        return value switch
        {
            int intValue => intValue,
            null => throw new NotSupportedException($"{methodName} requires a non-null integer value."),
            _ => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    public static TEnum ReadEnum<TEnum>(Expression expression, string methodName)
        where TEnum : struct, Enum
    {
        var value = Read(expression);

        return value switch
        {
            TEnum typed => typed,
            null => throw new NotSupportedException($"{methodName} requires a non-null enum value."),
            _ => (TEnum)Enum.ToObject(typeof(TEnum), value)
        };
    }

    private static object? ReadMember(MemberExpression expression)
    {
        var instance = expression.Expression is null ? null : Read(expression.Expression);
        var reader = MemberReaders.GetOrAdd(expression.Member, CreateMemberReader);

        return reader(instance);
    }

    private static Func<object?, object?> CreateMemberReader(MemberInfo member) => member switch
    {
        FieldInfo field => CreateFieldReader(field),
        PropertyInfo property => CreatePropertyReader(property),
        _ => throw new NotSupportedException($"Member '{member.Name}' cannot be evaluated as a captured query value.")
    };

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

    private static Array? ReadKnownMethodCall(MethodCallExpression expression)
    {
        if (expression.Object is null &&
            expression.Method.DeclaringType == typeof(Array) &&
            expression.Method.Name == nameof(Array.Empty) &&
            expression.Method.IsGenericMethod &&
            expression.Arguments.Count == 0)
        {
            var elementType = expression.Method.GetGenericArguments()[0];
            return EmptyArrays.GetOrAdd(elementType, static type => Array.CreateInstance(type, 0));
        }

        if (expression.Object is null &&
            expression.Method.DeclaringType == typeof(Enumerable) &&
            expression.Method.Name == nameof(Enumerable.Empty) &&
            expression.Method.IsGenericMethod &&
            expression.Arguments.Count == 0)
        {
            var elementType = expression.Method.GetGenericArguments()[0];
            return EmptyArrays.GetOrAdd(elementType, static type => Array.CreateInstance(type, 0));
        }

        throw new NotSupportedException(
            $"Method call '{expression.Method.DeclaringType?.FullName}.{expression.Method.Name}' cannot be evaluated as a captured query value.");
    }
}