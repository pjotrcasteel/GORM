using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Querying.Models;
using Gorm.Application.Querying.Translation;
using Gorm.Core.Loading;
using Gorm.Core.Primitives;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph query extensions.
/// </summary>
public static class GraphQueryExtensions
{
    private const string MethodOnlyInsideQueryExpressionTree = "This method is only used inside Gorm query expression trees.";

    /// <summary>
    /// Marks the query as no-tracking.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<T> AsNoTracking<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(AsNoTracking), parameterCount: 1).MakeGenericMethod(typeof(T));

        var call = Expression.Call(null, method, source.Expression);

        return source.Provider.CreateQuery<T>(call);
    }

    /// <summary>
    /// Marks the query as tracking.
    /// </summary>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<T> AsTracking<T>(this IQueryable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(AsTracking), parameterCount: 1).MakeGenericMethod(typeof(T));

        var call = Expression.Call(null, method, source.Expression);

        return source.Provider.CreateQuery<T>(call);
    }

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, IEnumerable<TRelated>> Include<TNode, TRelated>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression)
        where TNode : Node
        where TRelated : Node => Include(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, IEnumerable<TRelated>> Include<TNode, TRelated>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(IncludeCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return new GraphIncludableQueryable<TNode, IEnumerable<TRelated>>(source.Provider, call);
    }

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, TRelated> Include<TNode, TRelated>(this IQueryable<TNode> source, Expression<Func<TNode, TRelated?>> navigationExpression)
        where TNode : Node
        where TRelated : Node => Include(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, TRelated> Include<TNode, TRelated>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, TRelated?>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(IncludeReferenceCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return new GraphIncludableQueryable<TNode, TRelated>(source.Provider, call);
    }

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, IEnumerable<TRelated>> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, IEnumerable<TPreviousRelated>> source,
        Expression<Func<TPreviousRelated, IEnumerable<TRelated>>> navigationExpression)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node => ThenInclude(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, IEnumerable<TRelated>> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, IEnumerable<TPreviousRelated>> source,
        Expression<Func<TPreviousRelated, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(ThenIncludeCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TPreviousRelated), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return new GraphIncludableQueryable<TNode, IEnumerable<TRelated>>(source.Provider, call);
    }

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, IEnumerable<TRelated>> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, TPreviousRelated> source,
        Expression<Func<TPreviousRelated, IEnumerable<TRelated>>> navigationExpression)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node => ThenInclude(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, IEnumerable<TRelated>> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, TPreviousRelated> source,
        Expression<Func<TPreviousRelated, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(ThenIncludeAfterReferenceCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TPreviousRelated), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return new GraphIncludableQueryable<TNode, IEnumerable<TRelated>>(source.Provider, call);
    }

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, TRelated> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, TPreviousRelated> source,
        Expression<Func<TPreviousRelated, TRelated?>> navigationExpression)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node => ThenInclude(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, TRelated> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, TPreviousRelated> source,
        Expression<Func<TPreviousRelated, TRelated?>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(ThenIncludeReferenceAfterReferenceCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TPreviousRelated), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return new GraphIncludableQueryable<TNode, TRelated>(source.Provider, call);
    }

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, TRelated> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, IEnumerable<TPreviousRelated>> source,
        Expression<Func<TPreviousRelated, TRelated?>> navigationExpression)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node => ThenInclude(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the next relationship level.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TPreviousRelated">The type of t previous related.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IGraphIncludableQueryable<TNode, TRelated> ThenInclude<TNode, TPreviousRelated, TRelated>(
        this IGraphIncludableQueryable<TNode, IEnumerable<TPreviousRelated>> source,
        Expression<Func<TPreviousRelated, TRelated?>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(ThenIncludeReferenceCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TPreviousRelated), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return new GraphIncludableQueryable<TNode, TRelated>(source.Provider, call);
    }

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TNode> IncludeRelationship<TNode>(this IQueryable<TNode> source, string relationshipName)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            throw new ArgumentException("Relationship name cannot be null or whitespace.", nameof(relationshipName));
        }

        var method = GetGenericMethodDefinition(nameof(IncludeRelationship), parameterCount: 2, secondParameterType: typeof(string)).MakeGenericMethod(typeof(TNode));

        var call = Expression.Call(null, method, source.Expression, Expression.Constant(relationshipName));

        return source.Provider.CreateQuery<TNode>(call);
    }

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TNode> IncludeRelationship<TNode, TRelated>(this IQueryable<TNode> source, Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression)
        where TNode : Node
        where TRelated : Node => IncludeRelationship(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TNode> IncludeRelationship<TNode, TRelated>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false)).Build();

        var method = GetGenericMethodDefinition(
                nameof(IncludeRelationshipCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return source.Provider.CreateQuery<TNode>(call);
    }

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TNode> IncludeRelationshipWithEdges<TNode>(this IQueryable<TNode> source, string relationshipName)
        where TNode : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            throw new ArgumentException("Relationship name cannot be null or whitespace.", nameof(relationshipName));
        }

        var method = GetGenericMethodDefinition(nameof(IncludeRelationshipWithEdges), parameterCount: 2, secondParameterType: typeof(string)).MakeGenericMethod(typeof(TNode));

        var call = Expression.Call(null, method, source.Expression, Expression.Constant(relationshipName));

        return source.Provider.CreateQuery<TNode>(call);
    }

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TNode> IncludeRelationshipWithEdges<TNode, TRelated>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression)
        where TNode : Node
        where TRelated : Node => IncludeRelationshipWithEdges(source, navigationExpression, static x => x);

    /// <summary>
    /// Includes the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TNode> IncludeRelationshipWithEdges<TNode, TRelated>(
        this IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>> configure)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(configure);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var request = configure(new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: true)).Build();

        var method = GetGenericMethodDefinition(
                nameof(IncludeRelationshipWithEdgesCore),
                parameterCount: 3,
                secondParameterType: typeof(Expression<>),
                bindingFlags: BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(typeof(TNode), typeof(TRelated));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(navigationExpression), Expression.Constant(request));

        return source.Provider.CreateQuery<TNode>(call);
    }

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Outgoing<TEdge, TTo>(this IQueryable source)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(Outgoing), parameterCount: 1).MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(null, method, source.Expression);

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Outgoing<TEdge, TTo>(this IQueryable source, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(Outgoing), parameterCount: 2, secondParameterType: typeof(GraphTraversalSafeties))
            .MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(null, method, source.Expression, Expression.Constant(safety));

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Outgoing<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var method = GetGenericMethodDefinition(nameof(Outgoing), parameterCount: 2, secondParameterType: typeof(Expression<>)).MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(instance: null, method: method, arg0: source.Expression, arg1: Expression.Constant(predicate));

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Outgoing<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var method = GetGenericMethodDefinition(nameof(Outgoing), parameterCount: 3, secondParameterType: typeof(Expression<>)).MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(instance: null, method: method, arg0: source.Expression, arg1: Expression.Quote(predicate), arg2: Expression.Constant(safety));

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Incoming<TEdge, TTo>(this IQueryable source)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(Incoming), parameterCount: 1).MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(null, method, source.Expression);

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Incoming<TEdge, TTo>(this IQueryable source, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(Incoming), parameterCount: 2, secondParameterType: typeof(GraphTraversalSafeties))
            .MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(null, method, source.Expression, Expression.Constant(safety));

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Incoming<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var method = GetGenericMethodDefinition(nameof(Incoming), parameterCount: 2, secondParameterType: typeof(Expression<>)).MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(predicate));

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> Incoming<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);

        var method = GetGenericMethodDefinition(nameof(Incoming), parameterCount: 3, secondParameterType: typeof(Expression<>)).MakeGenericMethod(typeof(TEdge), typeof(TTo));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(predicate), Expression.Constant(safety));

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes then outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenOutgoing<TEdge, TTo>(this IQueryable source)
        where TEdge : Edge
        where TTo : Node => source.Outgoing<TEdge, TTo>();

    /// <summary>
    /// Executes then outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenOutgoing<TEdge, TTo>(this IQueryable source, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node => source.Outgoing<TEdge, TTo>(safety);

    /// <summary>
    /// Executes then outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenOutgoing<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
        where TTo : Node => source.Outgoing<TEdge, TTo>(predicate);

    /// <summary>
    /// Executes then outgoing.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenOutgoing<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node => source.Outgoing<TEdge, TTo>(predicate, safety);

    /// <summary>
    /// Executes then incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenIncoming<TEdge, TTo>(this IQueryable source)
        where TEdge : Edge
        where TTo : Node => source.Incoming<TEdge, TTo>();

    /// <summary>
    /// Executes then incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenIncoming<TEdge, TTo>(this IQueryable source, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node => source.Incoming<TEdge, TTo>(safety);

    /// <summary>
    /// Executes then incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenIncoming<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
        where TTo : Node => source.Incoming<TEdge, TTo>(predicate);

    /// <summary>
    /// Executes then incoming.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <param name="safety">The safety.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ThenIncoming<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate, GraphTraversalSafeties safety)
        where TEdge : Edge
        where TTo : Node => source.Incoming<TEdge, TTo>(predicate, safety);

    /// <summary>
    /// Executes outgoing where.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> OutgoingWhere<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
        where TTo : Node => source.Outgoing<TEdge, TTo>(predicate);

    /// <summary>
    /// Executes incoming where.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="predicate">The predicate.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> IncomingWhere<TEdge, TTo>(this IQueryable source, Expression<Func<TEdge, bool>> predicate)
        where TEdge : Edge
        where TTo : Node => source.Incoming<TEdge, TTo>(predicate);

    /// <summary>
    /// Executes from node.
    /// </summary>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TFrom> FromNode<TFrom>(this IQueryable source)
        where TFrom : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(FromNode), parameterCount: 1).MakeGenericMethod(typeof(TFrom));

        var call = Expression.Call(null, method, source.Expression);

        return source.Provider.CreateQuery<TFrom>(call);
    }

    /// <summary>
    /// Executes to node.
    /// </summary>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <param name="source">The source query.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTo> ToNode<TTo>(this IQueryable source)
        where TTo : Node
    {
        ArgumentNullException.ThrowIfNull(source);

        var method = GetGenericMethodDefinition(nameof(ToNode), parameterCount: 1).MakeGenericMethod(typeof(TTo));

        var call = Expression.Call(null, method, source.Expression);

        return source.Provider.CreateQuery<TTo>(call);
    }

    /// <summary>
    /// Executes select with edge.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TResult">The type of t result.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TResult> SelectWithEdge<TEdge, TNode, TResult>(this IQueryable<TNode> source, Expression<Func<TNode, TEdge, TResult>> selector)
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        var method = GetGenericMethodDefinition(nameof(SelectWithEdge), parameterCount: 2).MakeGenericMethod(typeof(TEdge), typeof(TNode), typeof(TResult));

        var call = Expression.Call(null, method, source.Expression, Expression.Quote(selector));

        return source.Provider.CreateQuery<TResult>(call);
    }

    #region Queryable marker methods
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> IncludeCore<TNode, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> IncludeReferenceCore<TNode, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TNode, TRelated?>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> ThenIncludeCore<TNode, TPreviousRelated, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TPreviousRelated, IEnumerable<TRelated>>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> ThenIncludeReferenceCore<TNode, TPreviousRelated, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TPreviousRelated, TRelated?>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> ThenIncludeAfterReferenceCore<TNode, TPreviousRelated, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TPreviousRelated, IEnumerable<TRelated>>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> ThenIncludeReferenceAfterReferenceCore<TNode, TPreviousRelated, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TPreviousRelated, TRelated?>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TPreviousRelated : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> IncludeRelationshipCore<TNode, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IQueryable<TNode> IncludeRelationshipWithEdgesCore<TNode, TRelated>(
        IQueryable<TNode> source,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        GraphIncludeRequest request)
        where TNode : Node
        where TRelated : Node
    {
        _ = source;
        _ = navigationExpression;
        _ = request;

        throw new NotSupportedException(MethodOnlyInsideQueryExpressionTree);
    }

    #endregion;

    private static MethodInfo GetGenericMethodDefinition(
        string name,
        int parameterCount,
        Type? secondParameterType = null,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static)
    {
        var matches = typeof(GraphQueryExtensions)
            .GetMethods(bindingFlags)
            .Where(m =>
                m.Name == name &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == parameterCount &&
                (secondParameterType is null || MatchSecondParameterType(m, secondParameterType)))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No generic method definition found for {name} with parameterCount={parameterCount}."),
            _ => throw new InvalidOperationException(
                $"Multiple generic method definitions found for {name} with parameterCount={parameterCount}.")
        };
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
}