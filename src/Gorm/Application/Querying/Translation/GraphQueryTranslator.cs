using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Querying.Models;

namespace Gorm.Application.Querying.Translation;

/// <summary>
/// Represents graph query translator.
/// </summary>
public static class GraphQueryTranslator
{
    private static readonly Dictionary<MethodInfo, GraphExtensionMethodDescriptor> GraphExtensionMethods = BuildGraphExtensionMethods();

    /// <summary>
    /// Translates the query.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The value.</returns>
    public static GraphQueryModel Translate(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var builder = new GraphQueryModelBuilder();

        Visit(expression, builder);

        return builder.Build();
    }

    private static void Visit(Expression expression, GraphQueryModelBuilder builder)
    {
        while (true)
        {
            switch (expression)
            {
                case GraphQueryRootExpression rootExpression:
                    builder.SetRoot(rootExpression.ElementType, rootExpression.ElementKind);
                    return;

                case MethodCallExpression methodCallExpression when TryVisitMethodCall(methodCallExpression, builder, out var next):
                    if (next is null)
                    {
                        return;
                    }

                    expression = next;
                    continue;

                case MethodCallExpression methodCallExpression:
                    throw new NotSupportedException(
                        $"Method '{methodCallExpression.Method.DeclaringType?.FullName}.{methodCallExpression.Method.Name}' is not supported yet.");

                default:
                    throw new NotSupportedException(
                        $"Expression type '{expression.GetType().FullName}' is not supported yet.");
            }
        }
    }

    private static bool TryVisitMethodCall(MethodCallExpression expression, GraphQueryModelBuilder builder, out Expression? next)
    {
        if (TryVisitQueryableMethodCall(expression, builder, out next))
        {
            return true;
        }

        if (TryVisitGraphExtensionMethodCall(expression, builder, out next))
        {
            return true;
        }

        next = null;
        return false;
    }

    private static bool TryVisitQueryableMethodCall(MethodCallExpression expression, GraphQueryModelBuilder builder, out Expression? next)
    {
        next = null;

        if (expression.Method.DeclaringType != typeof(Queryable) || !expression.Method.IsGenericMethod)
        {
            return false;
        }

        var methodName = expression.Method.Name;
        var genericArgumentCount = expression.Method.GetGenericArguments().Length;
        var parameterCount = expression.Method.GetParameters().Length;

        switch (methodName)
        {
            case nameof(Queryable.Where) when genericArgumentCount == 1 && parameterCount == 2:
                Visit(expression.Arguments[0], builder);
                builder.AddFilter(UnwrapQuotedLambda(expression.Arguments[1]));
                return true;

            case nameof(Queryable.OrderBy) when genericArgumentCount == 2 && parameterCount == 2:
            case nameof(Queryable.ThenBy) when genericArgumentCount == 2 && parameterCount == 2:
                Visit(expression.Arguments[0], builder);
                builder.AddOrdering(UnwrapQuotedLambda(expression.Arguments[1]), descending: false);
                return true;

            case nameof(Queryable.OrderByDescending) when genericArgumentCount == 2 && parameterCount == 2:
            case nameof(Queryable.ThenByDescending) when genericArgumentCount == 2 && parameterCount == 2:
                Visit(expression.Arguments[0], builder);
                builder.AddOrdering(UnwrapQuotedLambda(expression.Arguments[1]), descending: true);
                return true;

            case nameof(Queryable.Skip) when genericArgumentCount == 1 && parameterCount == 2 && expression.Arguments[1].Type == typeof(int):
                Visit(expression.Arguments[0], builder);
                builder.SetSkip(GraphExpressionValueReader.ReadInt(expression.Arguments[1], nameof(Queryable.Skip)));
                return true;

            case nameof(Queryable.Take) when genericArgumentCount == 1 && parameterCount == 2 && expression.Arguments[1].Type == typeof(int):
                Visit(expression.Arguments[0], builder);
                builder.SetTake(GraphExpressionValueReader.ReadInt(expression.Arguments[1], nameof(Queryable.Take)));
                return true;

            case nameof(Queryable.Select) when genericArgumentCount == 2 && parameterCount == 2:
                Visit(expression.Arguments[0], builder);
                builder.SetProjection(UnwrapQuotedLambda(expression.Arguments[1]));
                return true;

            case nameof(Queryable.Distinct) when genericArgumentCount == 1 && parameterCount == 1:
                Visit(expression.Arguments[0], builder);
                builder.SetDistinct();
                return true;

            default:
                return false;
        }
    }

    private static bool TryVisitGraphExtensionMethodCall(MethodCallExpression expression, GraphQueryModelBuilder builder, out Expression? next)
    {
        next = null;

        if (!expression.Method.IsGenericMethod)
        {
            return false;
        }

        var definition = expression.Method.GetGenericMethodDefinition();

        if (!GraphExtensionMethods.TryGetValue(definition, out var descriptor))
        {
            return false;
        }

        switch (descriptor.Kind)
        {
            case GraphExtensionMethodKind.AsNoTracking:
                Visit(expression.Arguments[0], builder);
                builder.SetTrackingMode(GraphQueryTrackingMode.NoTracking);
                return true;

            case GraphExtensionMethodKind.AsTracking:
                Visit(expression.Arguments[0], builder);
                builder.SetTrackingMode(GraphQueryTrackingMode.TrackAll);
                return true;

            case GraphExtensionMethodKind.SelectWithEdge:
                Visit(expression.Arguments[0], builder);
                AddEdgeNodeProjection(expression, builder);
                return true;

            case GraphExtensionMethodKind.FromNode:
                Visit(expression.Arguments[0], builder);
                builder.AddEdgeEndpointTraversal(GraphEdgeEndpoint.From, expression.Method.GetGenericArguments()[0]);
                return true;

            case GraphExtensionMethodKind.ToNode:
                Visit(expression.Arguments[0], builder);
                builder.AddEdgeEndpointTraversal(GraphEdgeEndpoint.To, expression.Method.GetGenericArguments()[0]);
                return true;

            case GraphExtensionMethodKind.Traversal:
                AddTraversal(expression, builder, descriptor);
                return true;

            default:
                return false;
        }
    }

    private static void AddEdgeNodeProjection(MethodCallExpression expression, GraphQueryModelBuilder builder)
    {
        var genericArguments = expression.Method.GetGenericArguments();

        builder.SetEdgeNodeProjection(genericArguments[0], genericArguments[1], UnwrapQuotedLambda(expression.Arguments[1]));
    }

    private static void AddTraversal(MethodCallExpression expression, GraphQueryModelBuilder builder, GraphExtensionMethodDescriptor descriptor)
    {
        Visit(expression.Arguments[0], builder);

        var genericArguments = expression.Method.GetGenericArguments();
        LambdaExpression? predicate = null;
        var safety = GraphTraversalSafeties.None;

        for (var i = 1; i < expression.Arguments.Count; i++)
        {
            if (TryUnwrapQuotedLambda(expression.Arguments[i], out var lambda))
            {
                predicate = lambda;
                continue;
            }

            if (expression.Arguments[i].Type == typeof(GraphTraversalSafeties))
            {
                safety = GraphExpressionValueReader.ReadEnum<GraphTraversalSafeties>(expression.Arguments[i], "Traversal safety");
            }
        }

        builder.AddTraversal(descriptor.Direction, genericArguments[0], genericArguments[1], predicate, safety);
    }

    private static LambdaExpression UnwrapQuotedLambda(Expression expression)
    {
        if (expression is UnaryExpression unaryExpression &&
            unaryExpression.NodeType == ExpressionType.Quote &&
            unaryExpression.Operand is LambdaExpression lambdaExpression)
        {
            return lambdaExpression;
        }

        if (expression is LambdaExpression directLambda)
        {
            return directLambda;
        }

        throw new NotSupportedException("Expected a lambda expression.");
    }

    private static bool TryUnwrapQuotedLambda(Expression expression, out LambdaExpression lambda)
    {
        if (expression is UnaryExpression unaryExpression &&
            unaryExpression.NodeType == ExpressionType.Quote &&
            unaryExpression.Operand is LambdaExpression quotedLambda)
        {
            lambda = quotedLambda;
            return true;
        }

        if (expression is LambdaExpression directLambda)
        {
            lambda = directLambda;
            return true;
        }

        lambda = null!;
        return false;
    }

    private static Dictionary<MethodInfo, GraphExtensionMethodDescriptor> BuildGraphExtensionMethods()
    {
        var result = new Dictionary<MethodInfo, GraphExtensionMethodDescriptor>
        {
            [GetMethod(nameof(GraphQueryExtensions.AsNoTracking), 1, 1)] = GraphExtensionMethodDescriptor.Simple(GraphExtensionMethodKind.AsNoTracking),
            [GetMethod(nameof(GraphQueryExtensions.AsTracking), 1, 1)] = GraphExtensionMethodDescriptor.Simple(GraphExtensionMethodKind.AsTracking),

            [GetMethod(nameof(GraphQueryExtensions.FromNode), 1, 1)] = GraphExtensionMethodDescriptor.Simple(GraphExtensionMethodKind.FromNode),
            [GetMethod(nameof(GraphQueryExtensions.ToNode), 1, 1)] = GraphExtensionMethodDescriptor.Simple(GraphExtensionMethodKind.ToNode),

            [GetSelectWithEdgeMethod()] = GraphExtensionMethodDescriptor.Simple(GraphExtensionMethodKind.SelectWithEdge),

            [GetMethod(nameof(GraphQueryExtensions.Outgoing), 1, 2)] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing),

            [GetMethod(nameof(GraphQueryExtensions.Outgoing), 2, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasPredicate: true),

            [GetMethod(nameof(GraphQueryExtensions.Outgoing), 2, 2, typeof(GraphTraversalSafeties))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasSafety: true, safetyArgumentIndex: 1),

            [GetMethod(nameof(GraphQueryExtensions.Outgoing), 3, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasPredicate: true, hasSafety: true, safetyArgumentIndex: 2),

            [GetMethod(nameof(GraphQueryExtensions.Incoming), 1, 2)] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming),

            [GetMethod(nameof(GraphQueryExtensions.Incoming), 2, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasPredicate: true),

            [GetMethod(nameof(GraphQueryExtensions.Incoming), 2, 2, typeof(GraphTraversalSafeties))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasSafety: true, safetyArgumentIndex: 1),

            [GetMethod(nameof(GraphQueryExtensions.Incoming), 3, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasPredicate: true, hasSafety: true, safetyArgumentIndex: 2),

            [GetMethod(nameof(GraphQueryExtensions.ThenOutgoing), 1, 2)] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing),

            [GetMethod(nameof(GraphQueryExtensions.ThenOutgoing), 2, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasPredicate: true),

            [GetMethod(nameof(GraphQueryExtensions.ThenOutgoing), 2, 2, typeof(GraphTraversalSafeties))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasSafety: true, safetyArgumentIndex: 1),

            [GetMethod(nameof(GraphQueryExtensions.ThenOutgoing), 3, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasPredicate: true, hasSafety: true, safetyArgumentIndex: 2),

            [GetMethod(nameof(GraphQueryExtensions.ThenIncoming), 1, 2)] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming),

            [GetMethod(nameof(GraphQueryExtensions.ThenIncoming), 2, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasPredicate: true),

            [GetMethod(nameof(GraphQueryExtensions.ThenIncoming), 2, 2, typeof(GraphTraversalSafeties))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasSafety: true, safetyArgumentIndex: 1),

            [GetMethod(nameof(GraphQueryExtensions.ThenIncoming), 3, 2, typeof(Expression<>))] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasPredicate: true, hasSafety: true, safetyArgumentIndex: 2),

            [GetMethod(nameof(GraphQueryExtensions.OutgoingWhere), 2, 2)] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Outgoing, hasPredicate: true),

            [GetMethod(nameof(GraphQueryExtensions.IncomingWhere), 2, 2)] =
                GraphExtensionMethodDescriptor.Traversal(GraphTraversalDirection.Incoming, hasPredicate: true)
        };

        return result;
    }

    private static MethodInfo GetMethod(string name, int parameterCount, int genericArgumentCount, Type? secondParameterType = null)
    {
        var methods = typeof(GraphQueryExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);
        MethodInfo? match = null;

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (method.Name != name ||
                !method.IsGenericMethodDefinition ||
                method.GetParameters().Length != parameterCount ||
                method.GetGenericArguments().Length != genericArgumentCount)
            {
                continue;
            }

            if (secondParameterType is not null && !MatchSecondParameterType(method, secondParameterType))
            {
                continue;
            }

            if (match is not null)
            {
                throw new InvalidOperationException(
                    $"Multiple generic method definitions found for '{typeof(GraphQueryExtensions).FullName}.{name}' with parameterCount={parameterCount}.");
            }

            match = method;
        }

        return match ?? throw new InvalidOperationException(
            $"No generic method definition found for '{typeof(GraphQueryExtensions).FullName}.{name}' with parameterCount={parameterCount}.");
    }

    private static MethodInfo GetSelectWithEdgeMethod()
    {
        var methods = typeof(GraphQueryExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);

        for (var i = 0; i < methods.Length; i++)
        {
            var method = methods[i];

            if (method.Name == nameof(GraphQueryExtensions.SelectWithEdge) &&
                method.IsGenericMethodDefinition &&
                method.GetGenericArguments().Length == 3 &&
                method.GetParameters().Length == 2)
            {
                return method;
            }
        }

        throw new InvalidOperationException(
            $"No generic method definition found for '{typeof(GraphQueryExtensions).FullName}.{nameof(GraphQueryExtensions.SelectWithEdge)}'.");
    }

    private static bool MatchSecondParameterType(MethodInfo methodInfo, Type secondParameterType)
    {
        var parameterType = methodInfo.GetParameters()[1].ParameterType;

        if (secondParameterType == typeof(Expression<>))
        {
            return parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Expression<>);
        }

        return parameterType == secondParameterType;
    }

    private enum GraphExtensionMethodKind
    {
        AsNoTracking,
        AsTracking,
        Traversal,
        FromNode,
        ToNode,
        SelectWithEdge
    }

    private readonly record struct GraphExtensionMethodDescriptor(
        GraphExtensionMethodKind Kind,
        GraphTraversalDirection Direction,
        bool HasPredicate,
        bool HasSafety,
        int SafetyArgumentIndex)
    {
        public static GraphExtensionMethodDescriptor Simple(GraphExtensionMethodKind kind) =>
            new(kind, default, HasPredicate: false, HasSafety: false, SafetyArgumentIndex: -1);

        public static GraphExtensionMethodDescriptor Traversal(
            GraphTraversalDirection direction,
            bool hasPredicate = false,
            bool hasSafety = false,
            int safetyArgumentIndex = -1) =>
            new(GraphExtensionMethodKind.Traversal, direction, hasPredicate, hasSafety, safetyArgumentIndex);
    }
}