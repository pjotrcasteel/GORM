using System.Reflection;
using Gorm.Application.Querying.Models;
using Gorm.Core.Paths;
using Gorm.Core.Primitives;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph path queryable extensions.
/// </summary>
public static class GraphPathQueryableExtensions
{
    private const string Outgoing = "Outgoing";
    private const string Incoming = "Incoming";
    private const string ThenOutgoing = "ThenOutgoing";
    private const string ThenIncoming = "ThenIncoming";
    private static readonly MethodInfo OutgoingMethodWithoutPredicateOrSafety = FindTraversalMethod(Outgoing, hasPredicate: false, hasSafety: false);
    private static readonly MethodInfo OutgoingMethodWithPredicate = FindTraversalMethod(Outgoing, hasPredicate: true, hasSafety: false);
    private static readonly MethodInfo OutgoingMethodWithSafety = FindTraversalMethod(Outgoing, hasPredicate: false, hasSafety: true);
    private static readonly MethodInfo OutgoingMethodWithPredicateAndSafety = FindTraversalMethod(Outgoing, hasPredicate: true, hasSafety: true);
    private static readonly MethodInfo IncomingMethodWithoutPredicateOrSafety = FindTraversalMethod(Incoming, hasPredicate: false, hasSafety: false);
    private static readonly MethodInfo IncomingMethodWithPredicate = FindTraversalMethod(Incoming, hasPredicate: true, hasSafety: false);
    private static readonly MethodInfo IncomingMethodWithSafety = FindTraversalMethod(Incoming, hasPredicate: false, hasSafety: true);
    private static readonly MethodInfo IncomingMethodWithPredicateAndSafety = FindTraversalMethod(Incoming, hasPredicate: true, hasSafety: true);
    private static readonly MethodInfo ThenOutgoingMethodWithoutPredicateOrSafety = FindTraversalMethod(ThenOutgoing, hasPredicate: false, hasSafety: false);
    private static readonly MethodInfo ThenOutgoingMethodWithPredicate = FindTraversalMethod(ThenOutgoing, hasPredicate: true, hasSafety: false);
    private static readonly MethodInfo ThenOutgoingMethodWithSafety = FindTraversalMethod(ThenOutgoing, hasPredicate: false, hasSafety: true);
    private static readonly MethodInfo ThenOutgoingMethodWithPredicateAndSafety = FindTraversalMethod(ThenOutgoing, hasPredicate: true, hasSafety: true);
    private static readonly MethodInfo ThenIncomingMethodWithoutPredicateOrSafety = FindTraversalMethod(ThenIncoming, hasPredicate: false, hasSafety: false);
    private static readonly MethodInfo ThenIncomingMethodWithPredicate = FindTraversalMethod(ThenIncoming, hasPredicate: true, hasSafety: false);
    private static readonly MethodInfo ThenIncomingMethodWithSafety = FindTraversalMethod(ThenIncoming, hasPredicate: false, hasSafety: true);
    private static readonly MethodInfo ThenIncomingMethodWithPredicateAndSafety = FindTraversalMethod(ThenIncoming, hasPredicate: true, hasSafety: true);

    /// <summary>
    /// Executes path.
    /// </summary>
    /// <typeparam name="TRoot">The type of t root.</typeparam>
    /// <typeparam name="TTarget">The type of t target.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="path">The path.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTarget> Path<TRoot, TTarget>(this IQueryable<TRoot> source, GraphPath<TRoot, TTarget> path)
        where TRoot : Node
        where TTarget : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Steps.Count == 0)
        {
            if (source is IQueryable<TTarget> sameTypeSource)
            {
                return path.DistinctResultsEnabled ? sameTypeSource.Distinct() : sameTypeSource;
            }

            throw new InvalidOperationException("An empty path can only be applied when the root and target node types are the same.");
        }

        object current = source;

        for (var i = 0; i < path.Steps.Count; i++)
        {
            var step = path.Steps[i];
            var isFirstStep = i == 0;

            current = ApplyStep(current, step, isFirstStep);
        }

        var result = (IQueryable<TTarget>)current;

        return path.DistinctResultsEnabled ? result.Distinct() : result;
    }

    /// <summary>
    /// Executes path.
    /// </summary>
    /// <typeparam name="TRoot">The type of t root.</typeparam>
    /// <typeparam name="TTarget">The type of t target.</typeparam>
    /// <param name="source">The source query.</param>
    /// <param name="build">The build.</param>
    /// <returns>The query.</returns>
    public static IQueryable<TTarget> Path<TRoot, TTarget>(this IQueryable<TRoot> source, Func<GraphPath<TRoot, TRoot>, GraphPath<TRoot, TTarget>> build)
        where TRoot : Node
        where TTarget : Node
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(build);

        var path = build(GraphPath.From<TRoot>());
        return source.Path(path);
    }

    private static object ApplyStep(object currentQuery, GraphPathStep step, bool isFirstStep)
    {
        var hasPredicate = step.EdgePredicate is not null;
        var hasSafety = step.Safety != GraphTraversalSafeties.None;
        var method = ResolveTraversalMethod(step.Direction, isFirstStep, hasPredicate, hasSafety);
        var closedMethod = method.MakeGenericMethod(step.EdgeType, step.NodeType);

        var arguments = BuildArguments(currentQuery, step, hasPredicate, hasSafety);
        return closedMethod.Invoke(null, arguments)!;
    }

    private static object?[] BuildArguments(object currentQuery, GraphPathStep step, bool hasPredicate, bool hasSafety)
    {
        if (!hasPredicate && !hasSafety)
        {
            return [currentQuery];
        }

        if (hasPredicate && !hasSafety)
        {
            return [currentQuery, step.EdgePredicate];
        }

        if (!hasPredicate)
        {
            return [currentQuery, step.Safety];
        }

        return [currentQuery, step.EdgePredicate, step.Safety];
    }

    private static MethodInfo ResolveTraversalMethod(GraphPathDirection direction, bool isFirstStep, bool hasPredicate, bool hasSafety)
    {
        if (direction == GraphPathDirection.Outgoing)
        {
            if (isFirstStep)
            {
                return ResolveOutgoingRootMethod(hasPredicate, hasSafety);
            }

            return ResolveOutgoingChainedMethod(hasPredicate, hasSafety);
        }

        if (isFirstStep)
        {
            return ResolveIncomingRootMethod(hasPredicate, hasSafety);
        }

        return ResolveIncomingChainedMethod(hasPredicate, hasSafety);
    }

    private static MethodInfo ResolveOutgoingRootMethod(bool hasPredicate, bool hasSafety)
    {
        if (hasPredicate)
        {
            return hasSafety ? OutgoingMethodWithPredicateAndSafety : OutgoingMethodWithPredicate;
        }

        return hasSafety ? OutgoingMethodWithSafety : OutgoingMethodWithoutPredicateOrSafety;
    }

    private static MethodInfo ResolveOutgoingChainedMethod(bool hasPredicate, bool hasSafety)
    {
        if (hasPredicate)
        {
            return hasSafety ? ThenOutgoingMethodWithPredicateAndSafety : ThenOutgoingMethodWithPredicate;
        }

        return hasSafety ? ThenOutgoingMethodWithSafety : ThenOutgoingMethodWithoutPredicateOrSafety;
    }

    private static MethodInfo ResolveIncomingRootMethod(bool hasPredicate, bool hasSafety)
    {
        if (hasPredicate)
        {
            return hasSafety ? IncomingMethodWithPredicateAndSafety : IncomingMethodWithPredicate;
        }

        return hasSafety ? IncomingMethodWithSafety : IncomingMethodWithoutPredicateOrSafety;
    }

    private static MethodInfo ResolveIncomingChainedMethod(bool hasPredicate, bool hasSafety)
    {
        if (hasPredicate)
        {
            return hasSafety ? ThenIncomingMethodWithPredicateAndSafety : ThenIncomingMethodWithPredicate;
        }

        return hasSafety ? ThenIncomingMethodWithSafety : ThenIncomingMethodWithoutPredicateOrSafety;
    }

    private static MethodInfo FindTraversalMethod(string methodName, bool hasPredicate, bool hasSafety)
    {
        foreach (var type in GetStaticExtensionTypes())
        {
            foreach (var method in GetCandidateMethods(type, methodName))
            {
                if (MatchesTraversalSignature(method, hasPredicate, hasSafety))
                {
                    return method;
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find traversal method '{methodName}' (hasPredicate: {hasPredicate}, hasSafety: {hasSafety}).");
    }

    private static IEnumerable<Type> GetStaticExtensionTypes() =>
        typeof(GraphPathQueryableExtensions)
            .Assembly
            .GetTypes()
            .Where(type => type.IsSealed && type.IsAbstract);

    private static IEnumerable<MethodInfo> GetCandidateMethods(Type type, string methodName) =>
        type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == methodName && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2);

    private static bool MatchesTraversalSignature(MethodInfo method, bool hasPredicate, bool hasSafety)
    {
        var parameters = method.GetParameters();

        return parameters.Length switch
        {
            1 => MatchesWithoutOptionalParameters(hasPredicate, hasSafety),
            2 => MatchesTwoParameterOverload(parameters[1].ParameterType, hasPredicate, hasSafety),
            3 => MatchesThreeParameterOverload(parameters, hasPredicate, hasSafety),
            _ => false
        };
    }

    private static bool MatchesWithoutOptionalParameters(bool hasPredicate, bool hasSafety) =>
        !hasPredicate && !hasSafety;

    private static bool MatchesTwoParameterOverload(Type secondParameterType, bool hasPredicate, bool hasSafety)
    {
        if (hasPredicate && !hasSafety)
        {
            return IsExpressionParameter(secondParameterType);
        }

        if (!hasPredicate && hasSafety)
        {
            return secondParameterType == typeof(GraphTraversalSafeties);
        }

        return false;
    }

    private static bool MatchesThreeParameterOverload(ParameterInfo[] parameters, bool hasPredicate, bool hasSafety) =>
        hasPredicate &&
        hasSafety &&
        IsExpressionParameter(parameters[1].ParameterType) &&
        parameters[2].ParameterType == typeof(GraphTraversalSafeties);

    private static bool IsExpressionParameter(Type parameterType) =>
        parameterType.IsGenericType &&
        parameterType.GetGenericTypeDefinition() == typeof(System.Linq.Expressions.Expression<>);
}