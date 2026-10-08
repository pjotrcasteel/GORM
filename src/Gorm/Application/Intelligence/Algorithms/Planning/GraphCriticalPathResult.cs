using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Represents a Critical Path Method analysis result.
/// </summary>
public sealed class GraphCriticalPathResult
{
    private readonly Dictionary<Guid, GraphCriticalPathNodeSchedule> _scheduleByNodeId;

    internal GraphCriticalPathResult(
        double projectDuration,
        IReadOnlyList<GraphCriticalPathNodeSchedule> schedule,
        IReadOnlyList<Node> criticalNodes,
        IReadOnlyList<Edge> criticalEdges,
        IReadOnlyList<Node> nodes,
        IReadOnlyList<Edge> edges)
    {
        ProjectDuration = projectDuration;
        Schedule = schedule;
        CriticalNodes = criticalNodes;
        CriticalEdges = criticalEdges;
        Nodes = nodes;
        Edges = edges;
        _scheduleByNodeId = schedule.ToDictionary(item => item.Node.Id);
    }

    /// <summary>
    /// Gets the earliest finish time of the complete projection.
    /// </summary>
    public double ProjectDuration { get; }

    /// <summary>
    /// Gets the schedule in deterministic topological order.
    /// </summary>
    public IReadOnlyList<GraphCriticalPathNodeSchedule> Schedule { get; }

    /// <summary>
    /// Gets all zero-slack nodes.
    /// </summary>
    public IReadOnlyList<Node> CriticalNodes { get; }

    /// <summary>
    /// Gets all dependency edges that can participate in a critical path.
    /// </summary>
    public IReadOnlyList<Edge> CriticalEdges { get; }

    /// <summary>
    /// Gets one deterministic primary critical path as ordered nodes.
    /// </summary>
    public IReadOnlyList<Node> Nodes { get; }

    /// <summary>
    /// Gets one deterministic primary critical path as ordered dependency edges.
    /// </summary>
    public IReadOnlyList<Edge> Edges { get; }

    /// <summary>
    /// Gets the number of dependency hops in the primary critical path.
    /// </summary>
    public int HopCount => Edges.Count;

    /// <summary>
    /// Gets the schedule for a projected node identifier.
    /// </summary>
    public GraphCriticalPathNodeSchedule GetNodeSchedule(Guid nodeId) =>
        _scheduleByNodeId.TryGetValue(nodeId, out var schedule)
            ? schedule
            : throw new KeyNotFoundException($"Node '{nodeId}' has no critical-path schedule.");
}