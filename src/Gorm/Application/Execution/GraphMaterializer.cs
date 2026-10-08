using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Context;
using Gorm.Application.Querying.Models;
using Gorm.Core.Primitives;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph materializer.
/// </summary>
public sealed class GraphMaterializer
{
    private static readonly ConcurrentDictionary<Type, ObjectMaterializationPlan> ObjectPlans = new();

    /// <summary>
    /// Executes materialize list async.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<List<T>> MaterializeListAsync<T>(GraphContext context, DbDataReader reader, GraphQueryModel queryModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(queryModel);
        return MaterializeListCoreAsync<T>(context, reader, queryModel, cancellationToken);
    }

    private static async Task<List<T>> MaterializeListCoreAsync<T>(GraphContext context, DbDataReader reader, GraphQueryModel queryModel, CancellationToken cancellationToken)
    {
        if (queryModel.Projection is null &&
            (typeof(Node).IsAssignableFrom(typeof(T)) || typeof(Edge).IsAssignableFrom(typeof(T))))
        {
            return queryModel.TrackingMode == GraphQueryTrackingMode.NoTracking
                ? await MaterializePlainObjectListAsync<T>(reader, cancellationToken)
                : await MaterializeTrackedEntityListAsync<T>(context, reader, queryModel, cancellationToken);
        }

        return queryModel.Projection switch
        {
            null => await MaterializePlainObjectListAsync<T>(reader, cancellationToken),
            GraphScalarProjection => await MaterializeScalarListAsync<T>(reader, cancellationToken),
            GraphObjectProjection objectProjection => await MaterializeObjectProjectionListAsync<T>(reader, objectProjection, cancellationToken),
            GraphConstructorProjection constructorProjection => await MaterializeConstructorProjectionListAsync<T>(reader, constructorProjection, cancellationToken),
            _ => throw new NotSupportedException($"Projection '{queryModel.Projection.GetType().FullName}' is not supported.")
        };
    }

    private static async Task<List<T>> MaterializeTrackedEntityListAsync<T>(
        GraphContext context,
        DbDataReader reader,
        GraphQueryModel queryModel,
        CancellationToken cancellationToken)
    {
        var entityType = typeof(T);
        var plan = GetPlan(entityType);
        var ordinals = ReadOrdinals(reader);
        var bindings = CreatePropertyBindings(plan, ordinals);
        var keyPropertyName = GetKeyPropertyName(context, queryModel);

        if (!ordinals.TryGetValue(keyPropertyName, out var keyOrdinal))
        {
            throw new InvalidOperationException($"Key column '{keyPropertyName}' was not returned for '{entityType.FullName}'.");
        }

        var result = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var keyValue = ReadValue(reader, keyOrdinal) ?? throw new InvalidOperationException($"Materialized entity '{entityType.FullName}' has a null key value.");

            if (context.ChangeTracker.TryGetTrackedEntityByKey(context.Model, entityType, keyValue) is T trackedEntity)
            {
                result.Add(trackedEntity);
                continue;
            }

            var item = (T)plan.Create();
            MaterializeProperties(reader, item!, bindings);

            context.Attach(item!);
            result.Add(item);
        }

        return result;
    }

    private static async Task<List<T>> MaterializePlainObjectListAsync<T>(DbDataReader reader, CancellationToken cancellationToken)
    {
        var plan = GetPlan(typeof(T));
        var ordinals = ReadOrdinals(reader);
        var bindings = CreatePropertyBindings(plan, ordinals);
        var result = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = (T)plan.Create();

            MaterializeProperties(reader, item!, bindings);
            result.Add(item);
        }

        return result;
    }

    private static async Task<List<T>> MaterializeScalarListAsync<T>(DbDataReader reader, CancellationToken cancellationToken)
    {
        var result = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var value = ReadValue(reader, ordinal: 0);
            result.Add((T?)ConvertValue(value, typeof(T))!);
        }

        return result;
    }

    private static async Task<List<T>> MaterializeObjectProjectionListAsync<T>(DbDataReader reader, GraphObjectProjection projection, CancellationToken cancellationToken)
    {
        var plan = GetPlan(typeof(T));
        var ordinals = ReadOrdinals(reader);
        var bindings = CreateObjectProjectionBindings(plan, ordinals, projection);
        var result = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = (T)plan.Create();

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];

                if (binding.NestedPlan is not null)
                {
                    binding.Property.Set(item!, MaterializeNestedObject(reader, binding.NestedPlan));
                    continue;
                }

                if (binding.Ordinal is int ordinal)
                {
                    var value = ReadValue(reader, ordinal);
                    binding.Property.Set(item!, ConvertValue(value, binding.Property.PropertyType));
                }
            }

            result.Add(item);
        }

        return result;
    }

    private static async Task<List<T>> MaterializeConstructorProjectionListAsync<T>(DbDataReader reader, GraphConstructorProjection projection, CancellationToken cancellationToken)
    {
        var ordinals = ReadOrdinals(reader);
        var bindings = CreateConstructorParameterBindings(projection, ordinals);
        var result = new List<T>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var arguments = new object?[bindings.Length];

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];

                if (binding.NestedPlan is not null)
                {
                    arguments[i] = MaterializeNestedObject(reader, binding.NestedPlan);
                    continue;
                }

                var value = ReadValue(reader, binding.Ordinal);
                arguments[i] = ConvertValue(value, binding.ParameterType);
            }

            var item = projection.Constructor.Invoke(arguments);
            result.Add((T)item);
        }

        return result;
    }

    private static void MaterializeProperties(DbDataReader reader, object item, IReadOnlyList<PropertyColumnBinding> bindings)
    {
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            var value = ReadValue(reader, binding.Ordinal);

            binding.Property.Set(item, ConvertValue(value, binding.Property.PropertyType));
        }
    }

    private static object? MaterializeNestedObject(DbDataReader reader, NestedObjectMaterializationPlan nestedPlan)
    {
        var item = nestedPlan.Plan.Create();
        var hasAnyValue = false;

        for (var i = 0; i < nestedPlan.Bindings.Length; i++)
        {
            var binding = nestedPlan.Bindings[i];
            var value = ReadValue(reader, binding.Ordinal);

            if (value is not null)
            {
                hasAnyValue = true;
            }

            binding.Property.Set(item, ConvertValue(value, binding.Property.PropertyType));
        }

        return hasAnyValue ? item : null;
    }

    private static PropertyColumnBinding[] CreatePropertyBindings(ObjectMaterializationPlan plan, Dictionary<string, int> ordinals)
    {
        var result = new List<PropertyColumnBinding>(plan.Properties.Length);

        for (var i = 0; i < plan.Properties.Length; i++)
        {
            var property = plan.Properties[i];

            if (ordinals.TryGetValue(property.Name, out var ordinal))
            {
                result.Add(new PropertyColumnBinding(property, ordinal));
            }
        }

        return [.. result];
    }

    private static ObjectProjectionPropertyBinding[] CreateObjectProjectionBindings(
        ObjectMaterializationPlan plan,
        Dictionary<string, int> ordinals,
        GraphObjectProjection projection)
    {
        var wholeEntityBindings = projection.Bindings.Where(x => x.IsWholeEntity).Select(x => x.TargetMemberName).ToHashSet(StringComparer.Ordinal);

        var result = new List<ObjectProjectionPropertyBinding>(plan.Properties.Length);

        for (var i = 0; i < plan.Properties.Length; i++)
        {
            var property = plan.Properties[i];

            if (wholeEntityBindings.Contains(property.Name))
            {
                var nestedPlan = CreateNestedObjectPlan(property.PropertyType, ordinals, property.Name + "__");
                result.Add(new ObjectProjectionPropertyBinding(property, Ordinal: null, nestedPlan));
                continue;
            }

            if (ordinals.TryGetValue(property.Name, out var ordinal))
            {
                result.Add(new ObjectProjectionPropertyBinding(property, ordinal, NestedPlan: null));
            }
        }

        return [.. result];
    }

    private static ConstructorParameterBinding[] CreateConstructorParameterBindings(GraphConstructorProjection projection, Dictionary<string, int> ordinals)
    {
        var result = new ConstructorParameterBinding[projection.Parameters.Count];

        for (var i = 0; i < projection.Parameters.Count; i++)
        {
            var parameter = projection.Parameters[i];

            if (parameter.IsWholeEntity)
            {
                result[i] = new ConstructorParameterBinding(
                    parameter.ParameterType,
                    Ordinal: -1,
                    CreateNestedObjectPlan(parameter.ParameterType, ordinals, parameter.ParameterName + "__"));

                continue;
            }

            if (!ordinals.TryGetValue(parameter.ParameterName, out var ordinal))
            {
                throw new InvalidOperationException($"Projection column '{parameter.ParameterName}' was not returned for constructor {nameof(projection)}.");
            }

            result[i] = new ConstructorParameterBinding(parameter.ParameterType, ordinal, NestedPlan: null);
        }

        return result;
    }

    private static NestedObjectMaterializationPlan CreateNestedObjectPlan(Type targetType, Dictionary<string, int> ordinals, string prefix)
    {
        var plan = GetPlan(targetType);
        var bindings = CreatePrefixedPropertyBindings(plan, ordinals, prefix);

        return new NestedObjectMaterializationPlan(plan, bindings);
    }

    private static PropertyColumnBinding[] CreatePrefixedPropertyBindings(ObjectMaterializationPlan plan, Dictionary<string, int> ordinals, string prefix)
    {
        var result = new List<PropertyColumnBinding>(plan.Properties.Length);

        for (var i = 0; i < plan.Properties.Length; i++)
        {
            var property = plan.Properties[i];
            var columnName = prefix + property.Name;

            if (ordinals.TryGetValue(columnName, out var ordinal))
            {
                result.Add(new PropertyColumnBinding(property, ordinal));
            }
        }

        return [.. result];
    }

    private static Dictionary<string, int> ReadOrdinals(DbDataReader reader)
    {
        var ordinals = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            ordinals[reader.GetName(i)] = i;
        }

        return ordinals;
    }

    private static object? ReadValue(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);

    private static string GetKeyPropertyName(GraphContext context, GraphQueryModel queryModel) => queryModel.CurrentElementKind switch
    {
        GraphQueryElementKind.Node => context.Model.GetNode(queryModel.CurrentElementType).KeyPropertyName,
        GraphQueryElementKind.Edge => context.Model.GetEdge(queryModel.CurrentElementType).KeyPropertyName,
        _ => throw new NotSupportedException($"Element kind '{queryModel.CurrentElementKind}' is not supported.")
    };

    private static ObjectMaterializationPlan GetPlan(Type type) => ObjectPlans.GetOrAdd(type, CreatePlan);

    private static ObjectMaterializationPlan CreatePlan(Type type)
    {
        var constructor = type.GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException(
            $"Type '{type.FullName}' must have a parameterless constructor for object materialization.");

        var createLambda = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructor), typeof(object)));
        var create = createLambda.Compile();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanWrite).Select(CreatePropertyPlan).ToArray();

        return new ObjectMaterializationPlan(create, properties);
    }

    private static PropertyMaterializationPlan CreatePropertyPlan(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");

        var body = Expression.Assign(Expression.Property(Expression.Convert(target, property.DeclaringType!), property), Expression.Convert(value, property.PropertyType));

        var setter = Expression.Lambda<Action<object, object?>>(body, target, value).Compile();

        return new PropertyMaterializationPlan(property.Name, property.PropertyType, setter);
    }

    private static readonly Dictionary<Type, Func<object, object?>> ValueConverters = new()
    {
        [typeof(Guid)] = ConvertGuid,
        [typeof(DateTimeOffset)] = ConvertDateTimeOffset,
        [typeof(DateOnly)] = ConvertDateOnly,
        [typeof(TimeOnly)] = ConvertTimeOnly,
        [typeof(TimeSpan)] = ConvertTimeSpan,
        [typeof(string)] = value => Convert.ToString(value, CultureInfo.InvariantCulture),
        [typeof(int)] = value => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        [typeof(long)] = value => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        [typeof(bool)] = value => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
        [typeof(decimal)] = value => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        [typeof(double)] = value => Convert.ToDouble(value, CultureInfo.InvariantCulture)
    };

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null || value == DBNull.Value)
        {
            return null;
        }

        var actualTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (actualTargetType == value.GetType())
        {
            return value;
        }

        if (actualTargetType.IsEnum)
        {
            return ConvertEnum(value, actualTargetType);
        }

        return ValueConverters.TryGetValue(actualTargetType, out var converter) ? converter(value) : Convert.ChangeType(value, actualTargetType, CultureInfo.InvariantCulture);
    }

    private static object ConvertEnum(object value, Type enumType)
    {
        if (value is string enumText)
        {
            return Enum.Parse(enumType, enumText, ignoreCase: true);
        }

        var numericValue = Convert.ChangeType(value, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture);
        return Enum.ToObject(enumType, numericValue!);
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static object ConvertGuid(object value) =>
        value switch
        {
            Guid guid => guid,
            string guidText => Guid.Parse(guidText, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot convert value '{value}' to Guid.")
        };

    private static object? ConvertDateTimeOffset(object value) =>
        value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(dateTime),
            string dateTimeOffsetText => DateTimeOffset.Parse(dateTimeOffsetText, CultureInfo.InvariantCulture),
            _ => Convert.ChangeType(value, typeof(DateTimeOffset), CultureInfo.InvariantCulture)
        };

    private static object ConvertDateOnly(object value) =>
        value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            string dateText => DateOnly.Parse(dateText, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot convert value '{value}' to DateOnly.")
        };

    private static object ConvertTimeOnly(object value) =>
        value switch
        {
            TimeOnly time => time,
            TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
            DateTime dateTime => TimeOnly.FromDateTime(dateTime),
            string timeText => TimeOnly.Parse(timeText, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot convert value '{value}' to TimeOnly.")
        };

    private static object ConvertTimeSpan(object value) =>
        value switch
        {
            TimeSpan timeSpan => timeSpan,
            TimeOnly time => time.ToTimeSpan(),
            string timeText => TimeSpan.Parse(timeText, CultureInfo.InvariantCulture),
            _ => Convert.ChangeType(value, typeof(TimeSpan), CultureInfo.InvariantCulture)
        };
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

    private sealed record ObjectMaterializationPlan(Func<object> Create, PropertyMaterializationPlan[] Properties);

    private sealed record PropertyMaterializationPlan(string Name, Type PropertyType, Action<object, object?> Set);

    private sealed record PropertyColumnBinding(PropertyMaterializationPlan Property, int Ordinal);

    private sealed record ObjectProjectionPropertyBinding(PropertyMaterializationPlan Property, int? Ordinal, NestedObjectMaterializationPlan? NestedPlan);

    private sealed record ConstructorParameterBinding(Type ParameterType, int Ordinal, NestedObjectMaterializationPlan? NestedPlan);

    private sealed record NestedObjectMaterializationPlan(ObjectMaterializationPlan Plan, PropertyColumnBinding[] Bindings);
}