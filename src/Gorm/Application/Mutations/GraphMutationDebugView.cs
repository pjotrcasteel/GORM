using System.Text;

namespace Gorm.Application.Mutations;

/// <summary>
/// Formats graph mutation plans.
/// </summary>
public static class GraphMutationDebugView
{
    /// <summary>
    /// Formats the graph mutation plan.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The debug view.</returns>
    public static string Format(GraphMutationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var builder = new StringBuilder();

        builder.AppendLine($"Graph mutation: {plan.Name}");

        if (!string.IsNullOrWhiteSpace(plan.CorrelationId))
        {
            builder.AppendLine($"CorrelationId: {plan.CorrelationId}");
        }

        if (!string.IsNullOrWhiteSpace(plan.SourceEventId))
        {
            builder.AppendLine($"SourceEventId: {plan.SourceEventId}");
        }

        builder.AppendLine($"Valid: {plan.Validation.IsValid}");
        builder.AppendLine();

        builder.AppendLine("Nodes:");

        foreach (var node in plan.Nodes)
        {
            builder.AppendLine($"- {node.Action} {node.Node.GetType().Name}/{node.Node.Id} operation={node.Operation} key={node.Key ?? "-"}");
        }

        builder.AppendLine();
        builder.AppendLine("Edges:");

        foreach (var edge in plan.Edges)
        {
            builder.AppendLine($"- {edge.Action} {edge.Edge.GetType().Name}/{edge.Edge.Id} {edge.From.GetType().Name}/{edge.From.Id}" +
                $" -> {edge.To.GetType().Name}/{edge.To.Id} operation={edge.Operation} key={edge.Key ?? "-"}");
        }

        if (!plan.Validation.IsValid)
        {
            builder.AppendLine();
            builder.AppendLine("Validation errors:");

            foreach (var error in plan.Validation.Errors)
            {
                builder.AppendLine($"- {error.Code}: {error.Message}");
            }
        }

        return builder.ToString();
    }
}