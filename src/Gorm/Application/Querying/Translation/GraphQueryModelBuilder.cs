using System.Linq.Expressions;
using System.Reflection;
using Gorm.Application.Querying.Models;
using Gorm;

namespace Gorm.Application.Querying.Translation;

/// <summary>
/// Represents graph query model builder.
/// </summary>
internal sealed class GraphQueryModelBuilder
{
    private GraphQueryModel? _model;

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="elementKind">The element kind.</param>
    public void SetRoot(Type elementType, GraphQueryElementKind elementKind)
    {
        ArgumentNullException.ThrowIfNull(elementType);

        _model ??= new GraphQueryModel
        {
            RootElementType = elementType,
            RootElementKind = elementKind
        };
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="predicate">The predicate.</param>
    public void AddFilter(LambdaExpression predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();

        var currentElementType = _model.CurrentElementType;
        var currentElementKind = _model.CurrentElementKind;
        var isProjected = _model.Projection is not null;

        _model.Steps.Add(new GraphFilterStep
        {
            InputElementType = currentElementType,
            InputElementKind = currentElementKind,
            OutputElementType = currentElementType,
            OutputElementKind = currentElementKind,
            Predicate = predicate,
            IsProjected = isProjected
        });
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="edgeType">The edge type.</param>
    /// <param name="toNodeType">The to node type.</param>
    /// <param name="edgePredicate">The edge predicate.</param>
    /// <param name="safety">The safety.</param>
    public void AddTraversal(
        GraphTraversalDirection direction,
        Type edgeType,
        Type toNodeType,
        LambdaExpression? edgePredicate = null,
        GraphTraversalSafeties safety = GraphTraversalSafeties.None)
    {
        ArgumentNullException.ThrowIfNull(edgeType);
        ArgumentNullException.ThrowIfNull(toNodeType);
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();
        EnsureGraphShapeOperationAllowed("graph traversal");

        if (_model.EnsureNotNull().CurrentElementKind != GraphQueryElementKind.Node)
        {
            throw new NotSupportedException("Outgoing(...) and Incoming(...) can only start from a node query.");
        }

        _model.Steps.Add(new GraphTraversalStep
        {
            Direction = direction,
            EdgeType = edgeType,
            EdgePredicate = edgePredicate,
            Safety = safety,
            InputElementType = _model.CurrentElementType,
            InputElementKind = GraphQueryElementKind.Node,
            OutputElementType = toNodeType,
            OutputElementKind = GraphQueryElementKind.Node
        });
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="nodeType">The node type.</param>
    public void AddEdgeEndpointTraversal(GraphEdgeEndpoint endpoint, Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();
        EnsureGraphShapeOperationAllowed("edge endpoint traversal");

        if (_model.EnsureNotNull().CurrentElementKind != GraphQueryElementKind.Edge)
        {
            throw new NotSupportedException("FromNode(...) and ToNode(...) can only start from an edge query.");
        }

        _model.Steps.Add(new GraphEdgeNodeTraversalStep
        {
            Endpoint = endpoint,
            InputElementType = _model.CurrentElementType,
            InputElementKind = GraphQueryElementKind.Edge,
            OutputElementType = nodeType,
            OutputElementKind = GraphQueryElementKind.Node
        });
    }

    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <param name="keySelector">The key selector.</param>
    /// <param name="descending">The descending.</param>
    public void AddOrdering(LambdaExpression keySelector, bool descending)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();

        if (_model.EnsureNotNull().IsDistinct)
        {
            throw new NotSupportedException("OrderBy(...) after Distinct() is not supported yet.");
        }

        var propertyName = GetPropertyName(keySelector);

        _model.Orderings.Add(new GraphOrdering
        {
            PropertyName = propertyName,
            Descending = descending,
            IsProjected = _model.Projection is not null
        });
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="selector">The selector.</param>
    public void SetProjection(LambdaExpression selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();

        if (_model.EnsureNotNull().Projection is not null)
        {
            throw new NotSupportedException("Only a single projection is supported.");
        }

        _model.Projection = BuildNodeOnlyProjection(selector);
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="edgeType">The edge type.</param>
    /// <param name="nodeType">The node type.</param>
    /// <param name="selector">The selector.</param>
    public void SetEdgeNodeProjection(Type edgeType, Type nodeType, LambdaExpression selector)
    {
        ArgumentNullException.ThrowIfNull(edgeType);
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();

        if (_model.EnsureNotNull().Projection is not null)
        {
            throw new NotSupportedException("Only a single projection is supported.");
        }

        if (_model.CurrentElementKind != GraphQueryElementKind.Node)
        {
            throw new NotSupportedException("SelectWithEdge(...) requires the current query element to be a node.");
        }

        if (_model.CurrentElementType != nodeType)
        {
            throw new NotSupportedException("SelectWithEdge(...) must target the current node type.");
        }

        if (_model.Steps.LastOrDefault() is not GraphTraversalStep lastTraversal)
        {
            throw new NotSupportedException("SelectWithEdge(...) requires the query to end with a traversal step.");
        }

        if (lastTraversal.EdgeType != edgeType)
        {
            throw new NotSupportedException(
                $"SelectWithEdge(...) expects the last traversed edge to be '{edgeType.Name}'.");
        }

        _model.Projection = BuildEdgeNodeProjection(selector);
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    public void SetDistinct()
    {
        ArgumentNullException.ThrowIfNull(_model);

        EnsureModel();

        if (_model.EnsureNotNull().IsDistinct)
        {
            throw new NotSupportedException("Only a single Distinct() is supported.");
        }

        if (_model.Orderings.Count > 0)
        {
            throw new NotSupportedException("Distinct() with OrderBy(...) is not supported yet.");
        }

        _model.IsDistinct = true;
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="count">The count.</param>
    public void SetSkip(int count)
    {
        EnsureModel();

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        _model.EnsureNotNull().SkipCount = count;
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="count">The count.</param>
    public void SetTake(int count)
    {
        EnsureModel();

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        _model.EnsureNotNull().TakeCount = count;
    }

    /// <summary>
    /// Sets the value.
    /// </summary>
    /// <param name="trackingMode">The tracking mode.</param>
    public void SetTrackingMode(GraphQueryTrackingMode trackingMode)
    {
        EnsureModel();
        _model.EnsureNotNull().TrackingMode = trackingMode;
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    public GraphQueryModel Build()
    {
        EnsureModel();
        return _model.EnsureNotNull();
    }

    private static GraphQueryProjection BuildNodeOnlyProjection(LambdaExpression selector)
    {
        var body = UnwrapConvert(selector.Body);

        if (body is MemberExpression memberExpression &&
            TryGetSourceParameterIndex(memberExpression, selector.Parameters, out var parameterIndex) &&
            parameterIndex == 0 &&
            memberExpression.Member is PropertyInfo propertyInfo)
        {
            return new GraphScalarProjection
            {
                ResultType = selector.ReturnType,
                SourcePropertyName = propertyInfo.Name,
                SourceKind = GraphProjectionSourceKind.Node
            };
        }

        if (body is MemberInitExpression memberInitExpression)
        {
            return BuildMemberInitProjection(memberInitExpression, selector.Parameters);
        }

        if (body is NewExpression newExpression)
        {
            return BuildConstructorProjection(newExpression, selector.Parameters);
        }

        throw new NotSupportedException("Supported Select(...) projections are scalar, member-init DTOs, constructor DTOs, and anonymous types.");
    }

    private static GraphQueryProjection BuildEdgeNodeProjection(LambdaExpression selector)
    {
        if (selector.Parameters.Count != 2)
        {
            throw new NotSupportedException("SelectWithEdge(...) requires exactly two lambda parameters.");
        }

        var body = UnwrapConvert(selector.Body);

        if (body is MemberExpression scalarMemberExpression &&
            TryCreateProjectionSource(scalarMemberExpression, selector.Parameters, out var scalarSource))
        {
            return new GraphScalarProjection
            {
                ResultType = selector.ReturnType,
                SourcePropertyName = scalarSource.SourcePropertyName,
                SourceKind = scalarSource.SourceKind
            };
        }

        if (body is MemberInitExpression memberInitExpression)
        {
            return BuildMemberInitProjection(memberInitExpression, selector.Parameters);
        }

        if (body is NewExpression newExpression)
        {
            return BuildConstructorProjection(newExpression, selector.Parameters);
        }

        throw new NotSupportedException("Supported SelectWithEdge(...) projections are scalar, member-init DTOs, constructor DTOs, and anonymous types.");
    }

    private static GraphObjectProjection BuildMemberInitProjection(MemberInitExpression memberInitExpression, IReadOnlyList<ParameterExpression> parameters)
    {
        var projection = new GraphObjectProjection
        {
            ResultType = memberInitExpression.Type
        };

        foreach (var binding in memberInitExpression.Bindings)
        {
            if (binding is not MemberAssignment assignment)
            {
                throw new NotSupportedException("Only simple member assignments are supported in projections.");
            }

            var assignedExpression = UnwrapConvert(assignment.Expression);

            if (assignedExpression is ParameterExpression parameterExpression &&
                TryGetParameterSource(parameterExpression, parameters, out var wholeEntitySource))
            {
                projection.Bindings.Add(new GraphProjectionBinding
                {
                    TargetMemberName = assignment.Member.Name,
                    SourceKind = wholeEntitySource,
                    SourcePropertyName = null,
                    IsWholeEntity = true
                });

                continue;
            }

            if (assignedExpression is MemberExpression sourceMemberExpression &&
                TryCreateProjectionSource(sourceMemberExpression, parameters, out var source))
            {
                projection.Bindings.Add(new GraphProjectionBinding
                {
                    TargetMemberName = assignment.Member.Name,
                    SourcePropertyName = source.SourcePropertyName,
                    SourceKind = source.SourceKind,
                    IsWholeEntity = false
                });

                continue;
            }

            throw new NotSupportedException("Only direct mapped node/edge property assignments and whole node/edge assignments are supported in projections.");
        }

        return projection;
    }

    private static GraphConstructorProjection BuildConstructorProjection(NewExpression newExpression, IReadOnlyList<ParameterExpression> parameters)
    {
        if (newExpression.Constructor is null)
        {
            throw new NotSupportedException("Projection constructor could not be resolved.");
        }

        var ctorParameters = newExpression.Constructor.GetParameters();

        if (ctorParameters.Length != newExpression.Arguments.Count)
        {
            throw new NotSupportedException("Projection constructor arguments could not be matched.");
        }

        var projection = new GraphConstructorProjection
        {
            ResultType = newExpression.Type,
            Constructor = newExpression.Constructor
        };

        for (var i = 0; i < newExpression.Arguments.Count; i++)
        {
            var argument = UnwrapConvert(newExpression.Arguments[i]);

            if (argument is ParameterExpression parameterExpression &&
                TryGetParameterSource(parameterExpression, parameters, out var wholeEntitySource))
            {
                projection.Parameters.Add(new GraphConstructorProjectionParameter
                {
                    ParameterName = ctorParameters[i].Name ?? $"arg{i}",
                    ParameterType = ctorParameters[i].ParameterType,
                    SourceKind = wholeEntitySource,
                    SourcePropertyName = null,
                    IsWholeEntity = true
                });

                continue;
            }

            if (argument is MemberExpression memberExpression &&
                TryCreateProjectionSource(memberExpression, parameters, out var source))
            {
                projection.Parameters.Add(new GraphConstructorProjectionParameter
                {
                    ParameterName = ctorParameters[i].Name ?? $"arg{i}",
                    ParameterType = ctorParameters[i].ParameterType,
                    SourcePropertyName = source.SourcePropertyName,
                    SourceKind = source.SourceKind,
                    IsWholeEntity = false
                });

                continue;
            }

            throw new NotSupportedException("Only direct mapped node/edge property arguments and whole node/edge arguments are supported in constructor projections.");
        }

        return projection;
    }

    private static bool TryGetParameterSource(ParameterExpression parameterExpression, IReadOnlyList<ParameterExpression> parameters, out GraphProjectionSourceKind sourceKind)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i] == parameterExpression)
            {
                sourceKind = i switch
                {
                    0 => GraphProjectionSourceKind.Node,
                    1 => GraphProjectionSourceKind.Edge,
                    _ => default
                };

                return i is 0 or 1;
            }
        }

        sourceKind = default;
        return false;
    }

    private static bool TryCreateProjectionSource(
        MemberExpression memberExpression,
        IReadOnlyList<ParameterExpression> parameters,
        out (string SourcePropertyName, GraphProjectionSourceKind SourceKind) source)
    {
        if (memberExpression.Member is not PropertyInfo propertyInfo ||
            !TryGetSourceParameterIndex(memberExpression, parameters, out var parameterIndex))
        {
            source = default;
            return false;
        }

        source = parameterIndex switch
        {
            0 => (propertyInfo.Name, GraphProjectionSourceKind.Node),
            1 => (propertyInfo.Name, GraphProjectionSourceKind.Edge),
            _ => default
        };

        return parameterIndex is 0 or 1;
    }

    private static bool TryGetSourceParameterIndex(MemberExpression memberExpression, IReadOnlyList<ParameterExpression> parameters, out int parameterIndex)
    {
        if (memberExpression.Expression is ParameterExpression parameterExpression)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] == parameterExpression)
                {
                    parameterIndex = i;
                    return true;
                }
            }
        }

        parameterIndex = -1;
        return false;
    }

    private static Expression UnwrapConvert(Expression expression)
    {
        while (expression is UnaryExpression unaryExpression &&
               unaryExpression.NodeType == ExpressionType.Convert)
        {
            expression = unaryExpression.Operand;
        }

        return expression;
    }

    private static string GetPropertyName(LambdaExpression expression)
    {
        var body = UnwrapConvert(expression.Body);

        if (body is MemberExpression memberExpression &&
            memberExpression.Member is PropertyInfo propertyInfo)
        {
            return propertyInfo.Name;
        }

        throw new NotSupportedException("Only simple property access expressions are supported.");
    }

    private void EnsureModel()
    {
        if (_model is null)
        {
            throw new InvalidOperationException("A graph query must start from a query root.");
        }
    }

    private void EnsureGraphShapeOperationAllowed(string operatorName)
    {
        if (_model?.Projection is not null)
        {
            throw new NotSupportedException(
                $"{operatorName} cannot be applied after Select(...), because the query is no longer graph-shaped.");
        }
    }
}