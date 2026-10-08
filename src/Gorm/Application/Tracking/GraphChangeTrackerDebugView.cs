using System.Text;

namespace Gorm.Application.Tracking;

/// <summary>
/// Represents graph change tracker debug view.
/// </summary>
public static class GraphChangeTrackerDebugView
{
    /// <summary>
    /// Executes format.
    /// </summary>
    /// <param name="changeTracker">The change tracker.</param>
    /// <returns>The value.</returns>
    public static string Format(GraphChangeTracker changeTracker)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);

        var builder = new StringBuilder();
        builder.AppendLine("GraphChangeTracker");
        builder.AppendLine($"Entries: {changeTracker.Entries.Count}");

        foreach (var entry in changeTracker.Entries.OrderBy(x => x.ClrType.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {entry.ClrType.Name} [{entry.State}] KeySet={entry.IsKeySet()}");
        }

        if (changeTracker.PendingEdgeConnections.Count > 0)
        {
            builder.AppendLine("Pending connections:");

            foreach (var connection in changeTracker.PendingEdgeConnections)
            {
                builder.AppendLine(
                    $"  {connection.FromNode.GetType().Name} -[{connection.Edge.GetType().Name}]-> {connection.ToNode.GetType().Name}");
            }
        }

        if (changeTracker.PendingEdgeDisconnections.Count > 0)
        {
            builder.AppendLine("Pending disconnections:");

            foreach (var disconnection in changeTracker.PendingEdgeDisconnections)
            {
                builder.AppendLine(
                    $"  {disconnection.FromNode.GetType().Name} -[{disconnection.EdgeType.Name}]-> {disconnection.ToNode.GetType().Name}");
            }
        }

        return builder.ToString();
    }
}