using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gorm.Application.Querying.Models;
using Gorm.Core.Loading;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;

namespace Gorm.Application.Context;

internal static class GraphRelationshipRuntimeCache
{
    private const string Value = "value";
    private static readonly MethodInfo LoadOutgoingMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadOutgoingAsync), parameterCount: 2);

    private static readonly MethodInfo LoadIncomingMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadIncomingAsync), parameterCount: 2);

    private static readonly MethodInfo LoadOutgoingFilteredMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadOutgoingAsync), parameterCount: 3);

    private static readonly MethodInfo LoadIncomingFilteredMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadIncomingAsync), parameterCount: 3);

    private static readonly MethodInfo LoadOutgoingWithEdgesMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadOutgoingWithEdgesAsync), parameterCount: 2);

    private static readonly MethodInfo LoadIncomingWithEdgesMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadIncomingWithEdgesAsync), parameterCount: 2);

    private static readonly MethodInfo LoadOutgoingWithEdgesFilteredMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadOutgoingWithEdgesAsync), parameterCount: 3);

    private static readonly MethodInfo LoadIncomingWithEdgesFilteredMethodDefinition =
        GetGenericInstanceMethodDefinition(typeof(GraphContext), nameof(GraphContext.LoadIncomingWithEdgesAsync), parameterCount: 3);

    private static readonly MethodInfo CastTaskResultMethodDefinition =
        typeof(GraphRelationshipRuntimeCache).GetMethod(nameof(CastTaskResultAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, Func<Edge>> EdgeFactories = [];
    private static readonly ConcurrentDictionary<Type, Func<object, Node?>> NodeExtractors = [];
    private static readonly ConcurrentDictionary<Type, Func<object, Edge?>> EdgeExtractors = [];
    private static readonly ConcurrentDictionary<EdgeResultKey, Func<Edge, Node, object>> EdgeResultFactories = [];
    private static readonly ConcurrentDictionary<RelationshipLoadInvokerKey, RelationshipLoadInvoker> RelationshipLoadInvokers = [];
    private static readonly ConditionalWeakTable<LambdaExpression, Func<object, bool>> ObjectPredicates = new();
    private static readonly ConditionalWeakTable<LambdaExpression, Func<object, object?>> ObjectKeySelectors = new();

    public delegate Task<object?> RelationshipLoadInvoker(
        GraphContext context,
        Node owner,
        LambdaExpression? edgePredicate,
        CancellationToken cancellationToken);

    public static Edge CreateEdge(Type edgeType) => EdgeFactories.GetOrAdd(edgeType, CreateEdgeFactory)();

    public static object CreateEdgeResult(Type edgeType, Type nodeType, Edge edge, Node node)
    {
        ArgumentNullException.ThrowIfNull(edgeType);
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(node);

        return EdgeResultFactories.GetOrAdd(new EdgeResultKey(edgeType, nodeType), CreateEdgeResultFactory)(edge, node);
    }

    public static Node? GetRelatedNode(object value) => value switch
    {
        Node node => node,
        _ => ExtractNodeFromEdgeResult(value)
    };

    public static Node? ExtractNodeFromEdgeResult(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return NodeExtractors.GetOrAdd(value.GetType(), CreateNodeExtractor)(value);
    }

    public static Edge? ExtractEdgeFromEdgeResult(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return EdgeExtractors.GetOrAdd(value.GetType(), CreateEdgeExtractor)(value);
    }

    public static Guid? TryGetEntityId(object value) => value switch
    {
        Node node => node.Id,
        Edge edge => edge.Id,
        _ => ExtractEdgeFromEdgeResult(value)?.Id
    };

    public static Func<object, bool> GetObjectPredicate(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ObjectPredicates.GetValue(expression, static key => CreateObjectPredicate(key));
    }

    public static Func<object, object?> GetObjectKeySelector(LambdaExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ObjectKeySelectors.GetValue(expression, static key => CreateObjectKeySelector(key));
    }

    public static GraphRelationshipMapping[] GetInverseRelationships(GraphModel model, GraphRelationshipMapping relationship, Type ownerType, Type relatedType)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(ownerType);
        ArgumentNullException.ThrowIfNull(relatedType);

        return model.GetInverseRelationships(relationship, ownerType, relatedType);
    }

    public static RelationshipLoadInvoker GetRelationshipLoadInvoker(GraphRelationshipMapping relationship, bool includeEdge, bool hasEdgePredicate)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var key = new RelationshipLoadInvokerKey(
            relationship.EdgeType,
            relationship.OwnerNodeType,
            relationship.RelatedNodeType,
            relationship.Direction,
            includeEdge,
            hasEdgePredicate);

        return RelationshipLoadInvokers.GetOrAdd(key, CreateRelationshipLoadInvoker);
    }

    private static Func<Edge> CreateEdgeFactory(Type edgeType)
    {
        if (!typeof(Edge).IsAssignableFrom(edgeType))
        {
            throw new InvalidOperationException(
                $"Relationship edge type '{edgeType.FullName}' is not an {nameof(Edge)} type.");
        }

        var constructor = edgeType.GetConstructor(Type.EmptyTypes) ?? throw new InvalidOperationException(
            $"Edge type '{edgeType.FullName}' must have a parameterless constructor.");

        var body = Expression.Convert(Expression.New(constructor), typeof(Edge));

        return Expression.Lambda<Func<Edge>>(body).Compile();
    }

    private static Func<Edge, Node, object> CreateEdgeResultFactory(EdgeResultKey key)
    {
        if (!typeof(Edge).IsAssignableFrom(key.EdgeType))
        {
            throw new InvalidOperationException(
                $"Relationship edge type '{key.EdgeType.FullName}' is not an {nameof(Edge)} type.");
        }

        if (!typeof(Node).IsAssignableFrom(key.NodeType))
        {
            throw new InvalidOperationException(
                $"Relationship node type '{key.NodeType.FullName}' is not a {nameof(Node)} type.");
        }

        var resultType = typeof(GraphRelatedEdgeResult<,>).MakeGenericType(key.EdgeType, key.NodeType);
        var edgeParameter = Expression.Parameter(typeof(Edge), "edge");
        var nodeParameter = Expression.Parameter(typeof(Node), "node");

        var body = Expression.MemberInit(
            Expression.New(resultType),
            Expression.Bind(
                resultType.GetProperty(nameof(GraphRelatedEdgeResult<,>.Node), BindingFlags.Public | BindingFlags.Instance)!,
                Expression.Convert(nodeParameter, key.NodeType)),
            Expression.Bind(
                resultType.GetProperty(nameof(GraphRelatedEdgeResult<,>.Edge), BindingFlags.Public | BindingFlags.Instance)!,
                Expression.Convert(edgeParameter, key.EdgeType)));

        return Expression.Lambda<Func<Edge, Node, object>>(Expression.Convert(body, typeof(object)), edgeParameter, nodeParameter).Compile();
    }

    private static Func<object, Node?> CreateNodeExtractor(Type valueType)
    {
        var property = valueType.GetProperty(nameof(GraphRelatedEdgeResult<,>.Node), BindingFlags.Public | BindingFlags.Instance);

        if (property is null || !typeof(Node).IsAssignableFrom(property.PropertyType))
        {
            return static _ => null;
        }

        var valueParameter = Expression.Parameter(typeof(object), Value);
        var body = Expression.TypeAs(Expression.Property(Expression.Convert(valueParameter, valueType), property), typeof(Node));

        return Expression.Lambda<Func<object, Node?>>(body, valueParameter).Compile();
    }

    private static Func<object, Edge?> CreateEdgeExtractor(Type valueType)
    {
        var property = valueType.GetProperty(nameof(GraphRelatedEdgeResult<,>.Edge), BindingFlags.Public | BindingFlags.Instance);

        if (property is null || !typeof(Edge).IsAssignableFrom(property.PropertyType))
        {
            return static _ => null;
        }

        var valueParameter = Expression.Parameter(typeof(object), Value);
        var body = Expression.TypeAs(Expression.Property(Expression.Convert(valueParameter, valueType), property), typeof(Edge));

        return Expression.Lambda<Func<object, Edge?>>(body, valueParameter).Compile();
    }

    private static Func<object, bool> CreateObjectPredicate(LambdaExpression expression)
    {
        if (expression.Parameters.Count != 1)
        {
            throw new InvalidOperationException("Predicate expressions must have exactly one parameter.");
        }

        if (expression.ReturnType != typeof(bool))
        {
            throw new InvalidOperationException(
                $"Predicate {nameof(expression)} must return '{typeof(bool).FullName}', but returned '{expression.ReturnType.FullName}'.");
        }

        var valueParameter = Expression.Parameter(typeof(object), Value);
        var body = Expression.Invoke(expression, Expression.Convert(valueParameter, expression.Parameters[0].Type));

        return Expression.Lambda<Func<object, bool>>(body, valueParameter).Compile();
    }

    private static Func<object, object?> CreateObjectKeySelector(LambdaExpression expression)
    {
        if (expression.Parameters.Count != 1)
        {
            throw new InvalidOperationException("Key selector expressions must have exactly one parameter.");
        }

        var valueParameter = Expression.Parameter(typeof(object), Value);
        var body = Expression.Convert(Expression.Invoke(expression, Expression.Convert(valueParameter, expression.Parameters[0].Type)), typeof(object));

        return Expression.Lambda<Func<object, object?>>(body, valueParameter).Compile();
    }

    private static RelationshipLoadInvoker CreateRelationshipLoadInvoker(RelationshipLoadInvokerKey key)
    {
        var methodDefinition = ResolveLoadMethod(key.Direction, key.IncludeEdge, key.HasEdgePredicate);
        var fromType = key.Direction == GraphTraversalDirection.Outgoing ? key.OwnerNodeType : key.RelatedNodeType;
        var toType = key.Direction == GraphTraversalDirection.Outgoing ? key.RelatedNodeType : key.OwnerNodeType;
        var ownerArgumentType = key.OwnerNodeType;
        var closedMethod = methodDefinition.MakeGenericMethod(key.EdgeType, fromType, toType);
        var contextParameter = Expression.Parameter(typeof(GraphContext), "context");
        var ownerParameter = Expression.Parameter(typeof(Node), "owner");
        var predicateParameter = Expression.Parameter(typeof(LambdaExpression), "edgePredicate");
        var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var ownerArgument = Expression.Convert(ownerParameter, ownerArgumentType);

        MethodCallExpression call;

        if (key.HasEdgePredicate)
        {
            var predicateType = typeof(Func<,>).MakeGenericType(key.EdgeType, typeof(bool));
            var expressionType = typeof(Expression<>).MakeGenericType(predicateType);

            call = Expression.Call(contextParameter, closedMethod, ownerArgument, Expression.Convert(predicateParameter, expressionType), cancellationTokenParameter);
        }
        else
        {
            call = Expression.Call(contextParameter, closedMethod, ownerArgument, cancellationTokenParameter);
        }

        var taskResultType = closedMethod.ReturnType.GetGenericArguments()[0];
        var castTaskResultMethod = CastTaskResultMethodDefinition.MakeGenericMethod(taskResultType);
        var body = Expression.Call(castTaskResultMethod, call);

        return Expression.Lambda<RelationshipLoadInvoker>(body, contextParameter, ownerParameter, predicateParameter, cancellationTokenParameter).Compile();
    }

    private static MethodInfo ResolveLoadMethod(GraphTraversalDirection direction, bool includeEdge, bool hasEdgeFilter) =>
        (direction, includeEdge, hasEdgeFilter) switch
        {
            (GraphTraversalDirection.Outgoing, true, true) => LoadOutgoingWithEdgesFilteredMethodDefinition,
            (GraphTraversalDirection.Outgoing, true, false) => LoadOutgoingWithEdgesMethodDefinition,
            (GraphTraversalDirection.Outgoing, false, true) => LoadOutgoingFilteredMethodDefinition,
            (GraphTraversalDirection.Outgoing, false, false) => LoadOutgoingMethodDefinition,

            (GraphTraversalDirection.Incoming, true, true) => LoadIncomingWithEdgesFilteredMethodDefinition,
            (GraphTraversalDirection.Incoming, true, false) => LoadIncomingWithEdgesMethodDefinition,
            (GraphTraversalDirection.Incoming, false, true) => LoadIncomingFilteredMethodDefinition,
            (GraphTraversalDirection.Incoming, false, false) => LoadIncomingMethodDefinition,

            _ => throw new NotSupportedException($"Traversal direction '{direction}' is not supported.")
        };

    private static async Task<object?> CastTaskResultAsync<TResult>(Task<TResult> task) => await task.ConfigureAwait(false);

    private static MethodInfo GetGenericInstanceMethodDefinition(Type declaringType, string name, int parameterCount)
    {
        var matches = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.Name == name && x.IsGenericMethodDefinition && x.GetParameters().Length == parameterCount)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No generic instance method definition found for '{declaringType.FullName}.{name}' with parameterCount={parameterCount}."),
            _ => throw new InvalidOperationException(
                $"Multiple generic instance method definitions found for '{declaringType.FullName}.{name}' with parameterCount={parameterCount}.")
        };
    }

    private readonly record struct EdgeResultKey(Type EdgeType, Type NodeType);

    private readonly record struct RelationshipLoadInvokerKey(
        Type EdgeType,
        Type OwnerNodeType,
        Type RelatedNodeType,
        GraphTraversalDirection Direction,
        bool IncludeEdge,
        bool HasEdgePredicate);
}