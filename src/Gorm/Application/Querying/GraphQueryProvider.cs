using System.Linq.Expressions;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Application.Querying.Translation;
using Gorm;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph query provider.
/// </summary>
public sealed class GraphQueryProvider : IQueryProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphQueryProvider"/> class.
    /// </summary>
    /// <param name="context">The graph context.</param>
    public GraphQueryProvider(GraphContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets the graph context this provider is associated with.
    /// </summary>
    public GraphContext Context { get; }

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The query.</returns>
    public IQueryable CreateQuery(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var elementType = GraphQueryProviderRuntimeCache.GetQueryableElementType(expression.Type) ??
            GraphQueryProviderRuntimeCache.GetEnumerableElementType(expression.Type) ??
            expression.Type.GetGenericArguments()[0];

        return GraphQueryProviderRuntimeCache.CreateQuery(this, expression, elementType);
    }

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <typeparam name="TElement">The type of t element.</typeparam>
    /// <param name="expression">The expression.</param>
    /// <returns>The query.</returns>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new GraphQueryable<TElement>(this, expression);
    }

    /// <summary>
    /// Executes execute.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public object? Execute(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return GraphQueryProviderRuntimeCache.Execute(this, expression, expression.Type);
    }

    /// <summary>
    /// Executes execute.
    /// </summary>
    /// <typeparam name="TResult">The type of t result.</typeparam>
    /// <param name="expression">The expression.</param>
    /// <returns>The result.</returns>
    public TResult Execute<TResult>(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var requestedType = typeof(TResult);
        var enumerableType = GraphQueryProviderRuntimeCache.GetEnumerableElementType(requestedType);

        if (enumerableType is not null)
        {
            var query = CreateQuery(expression);
            var list = GraphQueryProviderRuntimeCache.ToList(enumerableType, query);

            return (TResult)list.EnsureNotNull();
        }

        if (expression is MethodCallExpression methodCall &&
            methodCall.Method.DeclaringType == typeof(Queryable))
        {
            var (executed, value) = ExecuteScalarMethodCall<TResult>(methodCall);

            if (executed)
            {
                return value;
            }
        }

        throw new NotSupportedException($"Synchronous execution for result type '{requestedType.FullName}' is not supported.");
    }

    private (bool executed, TResult value) ExecuteScalarMethodCall<TResult>(MethodCallExpression methodCall)
    {
        ArgumentNullException.ThrowIfNull(methodCall);

        var sourceElementType = GraphQueryProviderRuntimeCache.GetQueryableElementType(methodCall.Arguments[0].Type);

        if (sourceElementType is null)
        {
            return CreateNotExecutedResult<TResult>();
        }

        var sourceQuery = CreateTypedQuery(methodCall.Arguments[0], sourceElementType);
        var prepared = PrepareScalarSourceQuery(methodCall, sourceQuery, sourceElementType);
        var result = ExecuteScalarMethodCallCore(methodCall, prepared.Query, prepared.ElementType);

        return result is null ? CreateNotExecutedResult<TResult>() : (true, (TResult)result);
    }

    private static (bool executed, TResult value) CreateNotExecutedResult<TResult>() =>
        (false, (TResult)(object?)null!);

    private static PreparedScalarQuery PrepareScalarSourceQuery(MethodCallExpression methodCall, IQueryable sourceQuery, Type sourceElementType)
    {
        if (methodCall.Arguments.Count != 2)
        {
            return new PreparedScalarQuery(sourceQuery, sourceElementType);
        }

        return RequiresPredicatePreprocessing(methodCall.Method.Name)
            ? PreparePredicateScalarQuery(methodCall, sourceQuery, sourceElementType)
            : PrepareSelectorScalarQuery(methodCall, sourceQuery, sourceElementType);
    }

    private static PreparedScalarQuery PreparePredicateScalarQuery(MethodCallExpression methodCall, IQueryable sourceQuery, Type sourceElementType)
    {
        var lambda = UnwrapQuotedLambda(methodCall.Arguments[1]);
        var filteredQuery = GraphQueryProviderRuntimeCache.ApplyWhere(sourceQuery, sourceElementType, lambda);

        return new PreparedScalarQuery(filteredQuery, sourceElementType);
    }

    private static PreparedScalarQuery PrepareSelectorScalarQuery(MethodCallExpression methodCall, IQueryable sourceQuery, Type sourceElementType)
    {
        if (!RequiresSelectorPreprocessing(methodCall.Method.Name))
        {
            return new PreparedScalarQuery(sourceQuery, sourceElementType);
        }

        var selector = UnwrapQuotedLambda(methodCall.Arguments[1]);
        var projectedQuery = GraphQueryProviderRuntimeCache.ApplySelect(sourceQuery, sourceElementType, selector, out var projectedElementType);

        return new PreparedScalarQuery(projectedQuery, projectedElementType);
    }

    private static bool RequiresPredicatePreprocessing(string methodName) =>
        methodName is nameof(Queryable.Any)
            or nameof(Queryable.Count)
            or nameof(Queryable.LongCount)
            or nameof(Queryable.First)
            or nameof(Queryable.FirstOrDefault)
            or nameof(Queryable.Single)
            or nameof(Queryable.SingleOrDefault)
            or nameof(Queryable.Last)
            or nameof(Queryable.LastOrDefault);

    private static bool RequiresSelectorPreprocessing(string methodName) =>
        methodName is nameof(Queryable.Min)
            or nameof(Queryable.Max)
            or nameof(Queryable.Sum)
            or nameof(Queryable.Average);

    private static object? ExecuteScalarMethodCallCore(MethodCallExpression methodCall, IQueryable sourceQuery, Type sourceElementType)
    {
        var methodName = methodCall.Method.Name;

        if (TryExecuteOperatorMethod(methodCall, sourceQuery, sourceElementType, methodName, out var operatorResult))
        {
            return operatorResult;
        }

        if (TryExecuteExecutionMethod(methodCall, sourceQuery, sourceElementType, methodName, out var executionResult))
        {
            return executionResult;
        }

        return null;
    }

    private static bool TryExecuteOperatorMethod(MethodCallExpression methodCall, IQueryable sourceQuery, Type sourceElementType, string methodName, out object? result)
    {
        switch (methodName)
        {
            case nameof(Queryable.Any):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.Any), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.All) when methodCall.Arguments.Count == 2:
                result = InvokeGeneric(
                    typeof(GraphQueryableOperatorsExtensions),
                    nameof(GraphQueryableOperatorsExtensions.All),
                    sourceElementType,
                    [sourceQuery, UnwrapQuotedLambda(methodCall.Arguments[1])]);
                return true;

            case nameof(Queryable.First):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.First), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.FirstOrDefault):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.FirstOrDefault), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Single):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.Single), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.SingleOrDefault):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.SingleOrDefault), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Last):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.Last), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.LastOrDefault):
                result = InvokeGeneric(typeof(GraphQueryableOperatorsExtensions), nameof(GraphQueryableOperatorsExtensions.LastOrDefault), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.ElementAt) when methodCall.Arguments.Count == 2:
                result = InvokeGeneric(
                    typeof(GraphQueryableOperatorsExtensions),
                    nameof(GraphQueryableOperatorsExtensions.ElementAt),
                    sourceElementType,
                    [sourceQuery, ReadConstantValue(methodCall.Arguments[1])]);
                return true;

            case nameof(Queryable.ElementAtOrDefault) when methodCall.Arguments.Count == 2:
                result = InvokeGeneric(
                    typeof(GraphQueryableOperatorsExtensions),
                    nameof(GraphQueryableOperatorsExtensions.ElementAtOrDefault),
                    sourceElementType,
                    [sourceQuery, ReadConstantValue(methodCall.Arguments[1])]);
                return true;

            default:
                result = null;
                return false;
        }
    }

    private static bool TryExecuteExecutionMethod(MethodCallExpression methodCall, IQueryable sourceQuery, Type sourceElementType, string methodName, out object? result)
    {
        switch (methodName)
        {
            case nameof(Queryable.Count):
                result = InvokeGeneric(typeof(GraphQueryableExecutionExtensions), nameof(GraphQueryableExecutionExtensions.Count), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.LongCount):
                result = InvokeGeneric(typeof(GraphQueryableExecutionExtensions), nameof(GraphQueryableExecutionExtensions.LongCount), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Min):
                result = InvokeGeneric(typeof(GraphQueryableExecutionExtensions), nameof(GraphQueryableExecutionExtensions.Min), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Max):
                result = InvokeGeneric(typeof(GraphQueryableExecutionExtensions), nameof(GraphQueryableExecutionExtensions.Max), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Sum):
                result = InvokeGeneric(typeof(GraphQueryableExecutionExtensions), nameof(GraphQueryableExecutionExtensions.Sum), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Average):
                result = InvokeGeneric(typeof(GraphQueryableExecutionExtensions), nameof(GraphQueryableExecutionExtensions.Average), sourceElementType, [sourceQuery]);
                return true;

            case nameof(Queryable.Contains) when methodCall.Arguments.Count == 2:
                result = InvokeGeneric(
                    typeof(GraphQueryableExecutionExtensions),
                    nameof(GraphQueryableExecutionExtensions.Contains),
                    sourceElementType,
                    [sourceQuery, ReadConstantValue(methodCall.Arguments[1])]);
                return true;

            default:
                result = null;
                return false;
        }
    }

    private IQueryable CreateTypedQuery(Expression expression, Type elementType) =>
        GraphQueryProviderRuntimeCache.CreateQuery(this, expression, elementType);

    private static object? InvokeGeneric(Type declaringType, string methodName, Type genericArgument, object?[] arguments) =>
        GraphQueryProviderRuntimeCache.InvokeStaticGeneric(declaringType, methodName, genericArgument, arguments);

    private static LambdaExpression UnwrapQuotedLambda(Expression expression)
    {
        if (expression is UnaryExpression unaryExpression && unaryExpression.NodeType == ExpressionType.Quote)
        {
            expression = unaryExpression.Operand;
        }

        return expression as LambdaExpression ?? throw new InvalidOperationException("Expected a lambda expression.");
    }

    private static object? ReadConstantValue(Expression expression) => GraphExpressionValueReader.Read(expression);

    private readonly record struct PreparedScalarQuery(IQueryable Query, Type ElementType);

    /// <summary>
    /// Translates the query.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public static GraphQueryModel Translate(Expression expression) =>
        GraphQueryTranslator.Translate(expression);
}