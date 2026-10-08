using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Execution;

namespace Gorm.Application.Querying;

/// <summary>
/// Caches closed generic query-provider helpers so runtime execution does not repeatedly scan reflection metadata.
/// </summary>
internal static class GraphQueryProviderRuntimeCache
{
    private static readonly ConcurrentDictionary<Type, Func<GraphQueryProvider, Expression, IQueryable>> CreateQueryInvokers = [];
    private static readonly ConcurrentDictionary<Type, Func<GraphQueryProvider, Expression, object?>> ExecuteInvokers = [];
    private static readonly ConcurrentDictionary<Type, Func<IQueryable, object>> ToListInvokers = [];
    private static readonly ConcurrentDictionary<Type, Func<IQueryable, LambdaExpression, IQueryable>> WhereInvokers = [];
    private static readonly ConcurrentDictionary<SelectInvokerKey, Func<IQueryable, LambdaExpression, IQueryable>> SelectInvokers = [];
    private static readonly ConcurrentDictionary<StaticGenericInvokerKey, Func<object?[], object?>> StaticGenericInvokers = [];

    private static readonly MethodInfo CreateQueryMethodDefinition = GetGenericMethodDefinition(
        typeof(GraphQueryProvider),
        nameof(GraphQueryProvider.CreateQuery),
        genericArgumentCount: 1,
        parameterCount: 1,
        BindingFlags.Public | BindingFlags.Instance);

    private static readonly MethodInfo ExecuteMethodDefinition = GetGenericMethodDefinition(
        typeof(GraphQueryProvider),
        nameof(GraphQueryProvider.Execute),
        genericArgumentCount: 1,
        parameterCount: 1,
        BindingFlags.Public | BindingFlags.Instance);

    private static readonly MethodInfo QueryableWhereMethodDefinition = GetQueryableWhereMethodDefinition();
    private static readonly MethodInfo QueryableSelectMethodDefinition = GetQueryableSelectMethodDefinition();

    private static readonly MethodInfo ToListMethodDefinition = GetGenericMethodDefinition(
        typeof(GraphQueryableExecutionExtensions),
        nameof(GraphQueryableExecutionExtensions.ToList),
        genericArgumentCount: 1,
        parameterCount: 1,
        BindingFlags.Public | BindingFlags.Static);

    public static IQueryable CreateQuery(GraphQueryProvider provider, Expression expression, Type elementType)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(elementType);

        return CreateQueryInvokers.GetOrAdd(elementType, CreateCreateQueryInvoker)(provider, expression);
    }

    public static object? Execute(GraphQueryProvider provider, Expression expression, Type resultType)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(resultType);

        return ExecuteInvokers.GetOrAdd(resultType, CreateExecuteInvoker)(provider, expression);
    }

    public static object ToList(Type elementType, IQueryable query)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        ArgumentNullException.ThrowIfNull(query);

        return ToListInvokers.GetOrAdd(elementType, CreateToListInvoker)(query);
    }

    public static IQueryable ApplyWhere(IQueryable sourceQuery, Type elementType, LambdaExpression lambda)
    {
        ArgumentNullException.ThrowIfNull(sourceQuery);
        ArgumentNullException.ThrowIfNull(elementType);
        ArgumentNullException.ThrowIfNull(lambda);

        return WhereInvokers.GetOrAdd(elementType, CreateWhereInvoker)(sourceQuery, lambda);
    }

    public static IQueryable ApplySelect(IQueryable sourceQuery, Type sourceElementType, LambdaExpression lambda, out Type resultElementType)
    {
        ArgumentNullException.ThrowIfNull(sourceQuery);
        ArgumentNullException.ThrowIfNull(sourceElementType);
        ArgumentNullException.ThrowIfNull(lambda);

        resultElementType = lambda.ReturnType;

        return SelectInvokers.GetOrAdd(new SelectInvokerKey(sourceElementType, resultElementType), CreateSelectInvoker)(sourceQuery, lambda);
    }

    public static object? InvokeStaticGeneric(Type declaringType, string methodName, Type genericArgument, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(genericArgument);
        ArgumentNullException.ThrowIfNull(arguments);

        var key = new StaticGenericInvokerKey(declaringType, methodName, genericArgument, arguments.Length);
        return StaticGenericInvokers.GetOrAdd(key, CreateStaticGenericInvoker)(arguments);
    }

    public static Type? GetQueryableElementType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            return type.GetGenericArguments()[0];
        }

        var interfaces = type.GetInterfaces();

        for (var i = 0; i < interfaces.Length; i++)
        {
            var item = interfaces[i];

            if (item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                return item.GetGenericArguments()[0];
            }
        }

        return null;
    }

    public static Type? GetEnumerableElementType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        var interfaces = type.GetInterfaces();

        for (var i = 0; i < interfaces.Length; i++)
        {
            var item = interfaces[i];

            if (item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return item.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static Func<GraphQueryProvider, Expression, IQueryable> CreateCreateQueryInvoker(Type elementType)
    {
        var provider = Expression.Parameter(typeof(GraphQueryProvider), "provider");
        var expression = Expression.Parameter(typeof(Expression), "expression");
        var method = CreateQueryMethodDefinition.MakeGenericMethod(elementType);
        var call = Expression.Call(provider, method, expression);

        return Expression.Lambda<Func<GraphQueryProvider, Expression, IQueryable>>(Expression.Convert(call, typeof(IQueryable)), provider, expression).Compile();
    }

    private static Func<GraphQueryProvider, Expression, object?> CreateExecuteInvoker(Type resultType)
    {
        var provider = Expression.Parameter(typeof(GraphQueryProvider), "provider");
        var expression = Expression.Parameter(typeof(Expression), "expression");
        var method = ExecuteMethodDefinition.MakeGenericMethod(resultType);
        var call = Expression.Call(provider, method, expression);

        return Expression.Lambda<Func<GraphQueryProvider, Expression, object?>>(Expression.Convert(call, typeof(object)), provider, expression).Compile();
    }

    private static Func<IQueryable, object> CreateToListInvoker(Type elementType)
    {
        var query = Expression.Parameter(typeof(IQueryable), "query");
        var queryType = typeof(IQueryable<>).MakeGenericType(elementType);
        var method = ToListMethodDefinition.MakeGenericMethod(elementType);
        var call = Expression.Call(method, Expression.Convert(query, queryType));

        return Expression.Lambda<Func<IQueryable, object>>(Expression.Convert(call, typeof(object)), query).Compile();
    }

    private static Func<IQueryable, LambdaExpression, IQueryable> CreateWhereInvoker(Type elementType)
    {
        var query = Expression.Parameter(typeof(IQueryable), "query");
        var lambda = Expression.Parameter(typeof(LambdaExpression), "lambda");
        var queryType = typeof(IQueryable<>).MakeGenericType(elementType);
        var predicateType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
        var expressionType = typeof(Expression<>).MakeGenericType(predicateType);
        var method = QueryableWhereMethodDefinition.MakeGenericMethod(elementType);

        var call = Expression.Call(method, Expression.Convert(query, queryType), Expression.Convert(lambda, expressionType));

        return Expression.Lambda<Func<IQueryable, LambdaExpression, IQueryable>>(Expression.Convert(call, typeof(IQueryable)), query, lambda).Compile();
    }

    private static Func<IQueryable, LambdaExpression, IQueryable> CreateSelectInvoker(SelectInvokerKey key)
    {
        var query = Expression.Parameter(typeof(IQueryable), "query");
        var lambda = Expression.Parameter(typeof(LambdaExpression), "lambda");
        var queryType = typeof(IQueryable<>).MakeGenericType(key.SourceElementType);
        var selectorType = typeof(Func<,>).MakeGenericType(key.SourceElementType, key.ResultElementType);
        var expressionType = typeof(Expression<>).MakeGenericType(selectorType);
        var method = QueryableSelectMethodDefinition.MakeGenericMethod(key.SourceElementType, key.ResultElementType);

        var call = Expression.Call(method, Expression.Convert(query, queryType), Expression.Convert(lambda, expressionType));

        return Expression.Lambda<Func<IQueryable, LambdaExpression, IQueryable>>(Expression.Convert(call, typeof(IQueryable)), query, lambda).Compile();
    }

    private static Func<object?[], object?> CreateStaticGenericInvoker(StaticGenericInvokerKey key)
    {
        var method = GetGenericMethodDefinition(
                key.DeclaringType,
                key.MethodName,
                genericArgumentCount: 1,
                key.ParameterCount,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(key.GenericArgument);

        var arguments = Expression.Parameter(typeof(object?[]), "arguments");
        var methodParameters = method.GetParameters();
        var callArguments = new Expression[methodParameters.Length];

        for (var i = 0; i < methodParameters.Length; i++)
        {
            callArguments[i] = Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(i)), methodParameters[i].ParameterType);
        }

        var call = Expression.Call(method, callArguments);

        Expression body = method.ReturnType == typeof(void) ? Expression.Block(call, Expression.Constant(null, typeof(object))) : Expression.Convert(call, typeof(object));

        return Expression.Lambda<Func<object?[], object?>>(body, arguments).Compile();
    }

    private static MethodInfo GetQueryableWhereMethodDefinition()
    {
        var methods = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static);

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (method.Name != nameof(Queryable.Where) ||
                !method.IsGenericMethodDefinition ||
                method.GetGenericArguments().Length != 1)
            {
                continue;
            }

            var parameters = method.GetParameters();

            if (parameters.Length != 2)
            {
                continue;
            }

            var lambdaType = parameters[1].ParameterType.GetGenericArguments()[0];
            var delegateParameters = lambdaType.GetGenericArguments();

            if (delegateParameters.Length == 2)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Queryable.Where<TSource>(IQueryable<TSource>, Expression<Func<TSource, bool>>) was not found.");
    }

    private static MethodInfo GetQueryableSelectMethodDefinition()
    {
        var methods = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static);

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (method.Name != nameof(Queryable.Select) ||
                !method.IsGenericMethodDefinition ||
                method.GetGenericArguments().Length != 2)
            {
                continue;
            }

            var parameters = method.GetParameters();

            if (parameters.Length != 2)
            {
                continue;
            }

            var lambdaType = parameters[1].ParameterType.GetGenericArguments()[0];
            var delegateParameters = lambdaType.GetGenericArguments();

            if (delegateParameters.Length == 2)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Queryable.Select<TSource, TResult>(IQueryable<TSource>, Expression<Func<TSource, TResult>>) was not found.");
    }

    private static MethodInfo GetGenericMethodDefinition(Type declaringType, string name, int genericArgumentCount, int parameterCount, BindingFlags bindingFlags)
    {
        var methods = declaringType.GetMethods(bindingFlags);
        MethodInfo? match = null;

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (method.Name != name ||
                !method.IsGenericMethodDefinition ||
                method.GetGenericArguments().Length != genericArgumentCount ||
                method.GetParameters().Length != parameterCount)
            {
                continue;
            }

            if (match is not null)
            {
                throw new InvalidOperationException(
                    $"Multiple generic method definitions found for '{declaringType.FullName}.{name}' " +
                    $"with genericArgumentCount={genericArgumentCount} and parameterCount={parameterCount}.");
            }

            match = method;
        }

        return match ?? throw new InvalidOperationException(
            $"No generic method definition found for '{declaringType.FullName}.{name}' with genericArgumentCount={genericArgumentCount} and parameterCount={parameterCount}.");
    }

    private readonly record struct SelectInvokerKey(Type SourceElementType, Type ResultElementType);

    private readonly record struct StaticGenericInvokerKey(
        Type DeclaringType,
        string MethodName,
        Type GenericArgument,
        int ParameterCount);
}