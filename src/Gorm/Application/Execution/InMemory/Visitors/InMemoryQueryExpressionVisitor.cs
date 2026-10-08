using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;

namespace Gorm.Application.Execution.InMemory.Visitors;

internal sealed class InMemoryQueryExpressionVisitor : ExpressionVisitor
{
    private const string QueryNotSupportedMessage =
        "In-memory query execution currently supports root node queries, standard LINQ composition, outgoing/incoming traversals, includes, and SelectWithEdge(...) " +
        "directly after traversal. Edge-root queries, FromNode(...), ToNode(...), and traversal safety flags are not supported yet.";

    private static readonly HashSet<string> TrackingMethodNames =
    [
        nameof(GraphQueryExtensions.AsNoTracking),
            nameof(GraphQueryExtensions.AsTracking)
    ];

    private readonly InMemoryGraphExecutionEngine _engine;

    public InMemoryQueryExpressionVisitor(InMemoryGraphExecutionEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is GraphQueryRootExpression rootExpression)
        {
            var rootQueryable = _engine.CreateRootQueryable(rootExpression);
            return Expression.Constant(rootQueryable, node.Type);
        }

        return base.Visit(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Method.DeclaringType == typeof(GraphQueryExtensions))
        {
            if (TrackingMethodNames.Contains(node.Method.Name))
            {
                return Visit(node.Arguments[0]) ?? throw new InvalidOperationException("Failed to rewrite the tracking call.");
            }

            if (IsSelectWithEdgeMethod(node.Method))
            {
                return RewriteSelectWithEdge(node);
            }

            if (IsTraversalMethod(node.Method))
            {
                return RewriteTraversal(node);
            }

            throw new NotSupportedException($"{QueryNotSupportedMessage} Unsupported method: {node.Method.Name}.");
        }

        return base.VisitMethodCall(node);
    }

    private ConstantExpression RewriteTraversal(MethodCallExpression node)
    {
        var visitedSource = Visit(node.Arguments[0]) ?? throw new InvalidOperationException("Failed to rewrite the traversal source.");

        var source = (IQueryable?)Evaluate(visitedSource) ?? throw new InvalidOperationException("Failed to evaluate the traversal source.");

        var genericArguments = node.Method.GetGenericArguments();
        var edgeType = genericArguments[0];
        var nodeType = genericArguments[1];
        var direction = ResolveDirection(node.Method.Name);

        var predicate = TryGetPredicate(node);
        var safety = TryGetSafety(node);

        var traversed = _engine.ExecuteTraversal(source, edgeType, nodeType, direction, predicate, safety);

        return Expression.Constant(traversed, typeof(IQueryable<>).MakeGenericType(nodeType));
    }

    private ConstantExpression RewriteSelectWithEdge(MethodCallExpression node)
    {
        if (node.Arguments[0] is not MethodCallExpression traversalCall ||
            !IsTraversalMethod(traversalCall.Method))
        {
            throw new NotSupportedException("In-memory SelectWithEdge(...) requires the source to end with a traversal call.");
        }

        var visitedSource = Visit(traversalCall.Arguments[0]) ?? throw new InvalidOperationException("Failed to rewrite the SelectWithEdge source.");

        var source = (IQueryable?)Evaluate(visitedSource) ?? throw new InvalidOperationException("Failed to evaluate the SelectWithEdge source.");

        var traversalGenericArguments = traversalCall.Method.GetGenericArguments();
        var edgeType = traversalGenericArguments[0];
        var nodeType = traversalGenericArguments[1];
        var direction = ResolveDirection(traversalCall.Method.Name);

        var predicate = TryGetPredicate(traversalCall);
        var safety = TryGetSafety(traversalCall);

        var traversed = _engine.ExecuteTraversalWithEdges(source, edgeType, nodeType, direction, predicate, safety);

        var selectGenericArguments = node.Method.GetGenericArguments();
        var resultType = selectGenericArguments[2];
        var selector = UnwrapLambda(node.Arguments[1]);

        var projected = InMemoryGraphExecutionEngine.ProjectSelectWithEdge(traversed, edgeType, nodeType, resultType, selector);

        return Expression.Constant(projected, typeof(IQueryable<>).MakeGenericType(resultType));
    }

    private static bool IsSelectWithEdgeMethod(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var candidate = method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;

        return candidate.DeclaringType == typeof(GraphQueryExtensions) &&
            candidate.Name == nameof(GraphQueryExtensions.SelectWithEdge) &&
            candidate.GetParameters().Length == 2 &&
            candidate.GetGenericArguments().Length == 3;
    }

    private static LambdaExpression UnwrapLambda(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression is UnaryExpression unary &&
            unary.NodeType == ExpressionType.Quote &&
            unary.Operand is LambdaExpression quotedLambda)
        {
            return quotedLambda;
        }

        if (expression is ConstantExpression constant &&
            constant.Value is LambdaExpression constantLambda)
        {
            return constantLambda;
        }

        return (LambdaExpression)(Evaluate(expression) ?? throw new InvalidOperationException("Could not evaluate the lambda expression."));
    }

    private static bool IsTraversalMethod(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.Name is nameof(GraphQueryExtensions.Outgoing) or
            nameof(GraphQueryExtensions.Incoming) or
            nameof(GraphQueryExtensions.ThenOutgoing) or
            nameof(GraphQueryExtensions.ThenIncoming);
    }

    private static GraphTraversalDirection ResolveDirection(string methodName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        return methodName is nameof(GraphQueryExtensions.Outgoing) or nameof(GraphQueryExtensions.ThenOutgoing)
            ? GraphTraversalDirection.Outgoing
            : GraphTraversalDirection.Incoming;
    }

    private static LambdaExpression? TryGetPredicate(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2)
        {
            return null;
        }

        var secondParameterType = node.Method.GetParameters()[1].ParameterType;

        if (!secondParameterType.IsGenericType ||
            secondParameterType.GetGenericTypeDefinition() != typeof(Expression<>))
        {
            return null;
        }

        var argument = node.Arguments[1];

        if (argument is UnaryExpression unary &&
            unary.NodeType == ExpressionType.Quote &&
            unary.Operand is LambdaExpression lambda)
        {
            return lambda;
        }

        if (argument is ConstantExpression constant &&
            constant.Value is LambdaExpression constantLambda)
        {
            return constantLambda;
        }

        return (LambdaExpression?)Evaluate(argument);
    }

    private static GraphTraversalSafeties TryGetSafety(MethodCallExpression node)
    {
        if (node.Arguments.Count == 0)
        {
            return GraphTraversalSafeties.None;
        }

        var lastParameter = node.Method.GetParameters().LastOrDefault();

        if (lastParameter?.ParameterType != typeof(GraphTraversalSafeties))
        {
            return GraphTraversalSafeties.None;
        }

        var argument = node.Arguments[^1];
        var value = Evaluate(argument);

        return value is GraphTraversalSafeties safety ? safety : GraphTraversalSafeties.None;
    }

    private static object? Evaluate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object)));

        return lambda.Compile().Invoke();
    }
}