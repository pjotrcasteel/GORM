using System.Globalization;
using System.Text;
using Gorm.Application.Querying.Diagnostics;
using Gorm.Application.Querying.Models;

namespace Gorm.Application.Querying;

/// <summary>
/// Represents graph query debug view.
/// </summary>
public static class GraphQueryDebugView
{
    /// <summary>
    /// Executes format.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The value.</returns>
    public static string Format(GraphQueryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var builder = new StringBuilder();

        AppendHeader(builder, model);
        AppendOrderings(builder, model);
        AppendPaging(builder, model);
        AppendProjection(builder, model);
        AppendSteps(builder, model);

        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, GraphQueryModel model)
    {
        builder.AppendLine("GraphQueryModel");
        builder.AppendLine(
            $"Root: {model.RootElementType.Name} [{model.RootElementKind}]");
        builder.AppendLine(
            $"Current: {model.CurrentElementType.Name} [{model.CurrentElementKind}]");
        builder.AppendLine($"Tracking: {model.TrackingMode}");
    }

    private static void AppendOrderings(StringBuilder builder, GraphQueryModel model)
    {
        if (model.Orderings.Count == 0)
        {
            return;
        }

        builder.AppendLine("Orderings:");

        foreach (var ordering in model.Orderings)
        {
            builder.AppendLine($"  {ordering.PropertyName} {(ordering.Descending ? "DESC" : "ASC")}");
        }
    }

    private static void AppendPaging(StringBuilder builder, GraphQueryModel model)
    {
        if (model.SkipCount is null && model.TakeCount is null)
        {
            return;
        }

        builder.AppendLine(
            $"Paging: Skip={model.SkipCount?.ToString(CultureInfo.InvariantCulture) ?? "null"}, " +
            $"Take={model.TakeCount?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
    }

    private static void AppendProjection(StringBuilder builder, GraphQueryModel model)
    {
        if (model.Projection is null)
        {
            return;
        }

        builder.AppendLine($"Projection: {model.Projection.GetType().Name}");
    }

    private static void AppendSteps(StringBuilder builder, GraphQueryModel model)
    {
        if (model.Steps.Count == 0)
        {
            return;
        }

        builder.AppendLine("Steps:");

        foreach (var step in model.Steps)
        {
            builder.AppendLine(FormatStep(step));
        }
    }

    private static string FormatStep(GraphQueryStep step)
    {
        return step switch
        {
            GraphFilterStep filter => FormatFilterStep(filter),
            GraphTraversalStep traversal => FormatTraversalStep(traversal),
            GraphEdgeNodeTraversalStep edgeTraversal => FormatEdgeNodeTraversalStep(edgeTraversal),
            _ => $"  {step.GetType().Name}"
        };
    }

    private static string FormatFilterStep(GraphFilterStep filter)
    {
        return $"  FILTER [{filter.InputElementType.Name}] {GraphExpressionDebugFormatter.Format(filter.Predicate)}";
    }

    private static string FormatTraversalStep(GraphTraversalStep traversal)
    {
        var traversalText =
            $"  {traversal.Direction.ToString().ToUpperInvariant()} " +
            $"{traversal.InputElementType.Name} -[{traversal.EdgeType.Name}]-> {traversal.OutputElementType.Name}";

        if (traversal.EdgePredicate is null)
        {
            return traversalText;
        }

        return traversalText + $" WHERE EDGE {GraphExpressionDebugFormatter.Format(traversal.EdgePredicate)}";
    }

    private static string FormatEdgeNodeTraversalStep(GraphEdgeNodeTraversalStep edgeTraversal)
    {
        return
            $"  EDGE-{edgeTraversal.Endpoint.ToString().ToUpperInvariant()} " +
            $"{edgeTraversal.InputElementType.Name} -> {edgeTraversal.OutputElementType.Name}";
    }
}