using System.Linq.Expressions;
using Gorm.Application.Querying;
using Gorm.Core.Loading;

namespace Gorm.Application.Querying.Translation;

/// <summary>
/// Represents graph include expression helper.
/// </summary>
internal static class GraphIncludeExpressionHelper
{
    /// <summary>
    /// Executes extract.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The result.</returns>
    public static GraphIncludeExtractionResult Extract(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var entries = new List<GraphIncludeChainEntry>();
        var current = expression;

        while (current is MethodCallExpression methodCall && TryReadIncludeChainEntry(methodCall, out var entry))
        {
            entries.Insert(0, entry);
            current = methodCall.Arguments[0];
        }

        return new GraphIncludeExtractionResult
        {
            QueryExpression = current,
            Includes = BuildIncludeTree(entries)
        };
    }

    private static bool TryReadIncludeChainEntry(MethodCallExpression methodCall, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GraphIncludeChainEntry? entry)
    {
        entry = null;

        if (!methodCall.Method.IsStatic || methodCall.Method.DeclaringType != typeof(GraphQueryExtensions))
        {
            return false;
        }

        if (!methodCall.Method.IsGenericMethod)
        {
            return false;
        }

        var methodName = methodCall.Method.Name;

        if (methodName == nameof(GraphQueryExtensions.IncludeRelationship) &&
            methodCall.Arguments.Count == 2 &&
            methodCall.Arguments[1].Type == typeof(string))
        {
            entry = new GraphIncludeChainEntry(false, new GraphIncludeRequest
            {
                Name = ReadRelationshipName(methodCall),
                NameKind = GraphIncludeNameKind.Relationship,
                IncludeEdge = false
            });

            return true;
        }

        if (methodName == nameof(GraphQueryExtensions.IncludeRelationshipWithEdges) &&
            methodCall.Arguments.Count == 2 &&
            methodCall.Arguments[1].Type == typeof(string))
        {
            entry = new GraphIncludeChainEntry(false, new GraphIncludeRequest
            {
                Name = ReadRelationshipName(methodCall),
                NameKind = GraphIncludeNameKind.Relationship,
                IncludeEdge = true
            });

            return true;
        }

        if (IsIncludeLikeMethod(methodName) || IsThenIncludeLikeMethod(methodName))
        {
            entry = new GraphIncludeChainEntry(IsThenIncludeLikeMethod(methodName), ReadIncludeRequest(methodCall));

            return true;
        }

        return false;
    }

    private static bool IsIncludeLikeMethod(string methodName) =>
        methodName is nameof(GraphQueryExtensions.Include)
            or nameof(GraphQueryExtensions.IncludeRelationship)
            or nameof(GraphQueryExtensions.IncludeRelationshipWithEdges)
            or "IncludeCore"
            or "IncludeReferenceCore"
            or "IncludeRelationshipCore"
            or "IncludeRelationshipWithEdgesCore";

    private static bool IsThenIncludeLikeMethod(string methodName) =>
        methodName is nameof(GraphQueryExtensions.ThenInclude)
            or "ThenIncludeCore"
            or "ThenIncludeReferenceCore"
            or "ThenIncludeAfterReferenceCore"
            or "ThenIncludeReferenceAfterReferenceCore";

    private static List<GraphIncludeRequest> BuildIncludeTree(IReadOnlyList<GraphIncludeChainEntry> entries)
    {
        var roots = new List<GraphIncludeRequest>();
        var path = new List<GraphIncludeRequest>();

        foreach (var entry in entries)
        {
            var request = Clone(entry.Request);

            if (!entry.IsThenInclude)
            {
                roots.Add(request);
                path.Clear();
                path.Add(request);
                continue;
            }

            if (path.Count == 0)
            {
                throw new InvalidOperationException("ThenInclude() must follow an Include().");
            }

            path[^1].AddChild(request);
            path.Add(request);
        }

        return roots;
    }

    private static GraphIncludeRequest Clone(GraphIncludeRequest request)
    {
        var clone = new GraphIncludeRequest
        {
            Name = request.Name,
            NameKind = request.NameKind,
            IncludeEdge = request.IncludeEdge,
            EdgePredicate = request.EdgePredicate,
            RelatedPredicate = request.RelatedPredicate,
            EdgeOrderBy = request.EdgeOrderBy,
            EdgeOrderDescending = request.EdgeOrderDescending,
            RelatedOrderBy = request.RelatedOrderBy,
            RelatedOrderDescending = request.RelatedOrderDescending,
            Skip = request.Skip,
            Take = request.Take,
        };

        if (request.Children.Count == 0)
        {
            return clone;
        }

        var children = new GraphIncludeRequest[request.Children.Count];

        for (var i = 0; i < request.Children.Count; i++)
        {
            children[i] = Clone(request.Children[i]);
        }

        clone.AddChildren(children);

        return clone;
    }

    private static GraphIncludeRequest ReadIncludeRequest(MethodCallExpression methodCall)
    {
        if (methodCall.Arguments.Count < 3)
        {
            throw new InvalidOperationException("Include request must carry navigation metadata.");
        }

        var navigationLambda = UnwrapLambda(methodCall.Arguments[1]);
        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationLambda);

        if (methodCall.Arguments[2] is not ConstantExpression constantExpression ||
            constantExpression.Value is not GraphIncludeRequest request)
        {
            throw new InvalidOperationException("Include request must be a constant GraphIncludeRequest.");
        }

        var clone = new GraphIncludeRequest
        {
            Name = propertyName,
            NameKind = GraphIncludeNameKind.Navigation,
            IncludeEdge = request.IncludeEdge,
            EdgePredicate = request.EdgePredicate,
            RelatedPredicate = request.RelatedPredicate,
            EdgeOrderBy = request.EdgeOrderBy,
            EdgeOrderDescending = request.EdgeOrderDescending,
            RelatedOrderBy = request.RelatedOrderBy,
            RelatedOrderDescending = request.RelatedOrderDescending,
            Skip = request.Skip,
            Take = request.Take,
        };

        if (request.Children.Count == 0)
        {
            return clone;
        }

        var children = new GraphIncludeRequest[request.Children.Count];

        for (var i = 0; i < request.Children.Count; i++)
        {
            children[i] = Clone(request.Children[i]);
        }

        clone.AddChildren(children);

        return clone;
    }

    private static LambdaExpression UnwrapLambda(Expression expression)
    {
        if (expression is UnaryExpression unaryExpression &&
            unaryExpression.NodeType == ExpressionType.Quote &&
            unaryExpression.Operand is LambdaExpression lambda)
        {
            return lambda;
        }

        if (expression is LambdaExpression directLambda)
        {
            return directLambda;
        }

        throw new InvalidOperationException(nameof(expression));
    }

    private static string ReadRelationshipName(MethodCallExpression methodCall)
    {
        if (methodCall.Arguments[1] is ConstantExpression constantExpression &&
            constantExpression.Value is string relationshipName &&
            !string.IsNullOrWhiteSpace(relationshipName))
        {
            return relationshipName;
        }

        throw new InvalidOperationException("Include relationship name must be a non-empty constant string.");
    }

    /// <summary>
    /// Represents graph include chain entry.
    /// </summary>
    private sealed record GraphIncludeChainEntry(bool IsThenInclude, GraphIncludeRequest Request);
}