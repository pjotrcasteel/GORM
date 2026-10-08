using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Describes one detached, scenario-only node or edge mutation.
/// </summary>
public sealed class GraphScenarioMutation
{
    private GraphScenarioMutation(GraphScenarioMutationParameters inputs)
    {
        var mutationKind = inputs.MutationKind;
        var entityKind = inputs.EntityKind;
        var entityId = inputs.EntityId;
        var nodeState = inputs.NodeState;
        var edgeState = inputs.EdgeState;
        var validAt = inputs.ValidAt;
        var recordedAt = inputs.RecordedAt;
        var description = inputs.Description;

        MutationKind = mutationKind;
        EntityKind = entityKind;
        EntityId = entityId;
        NodeState = nodeState;
        EdgeState = edgeState;
        ValidAt = validAt.ToUniversalTime();
        RecordedAt = recordedAt.ToUniversalTime();
        Description = description;
    }

    /// <summary>
    /// Gets the add, update or remove operation.
    /// </summary>
    public GraphScenarioMutationKind MutationKind { get; }

    /// <summary>
    /// Gets whether the target is a node or edge.
    /// </summary>
    public GraphWorldEntityKind EntityKind { get; }

    /// <summary>
    /// Gets the target entity identifier.
    /// </summary>
    public Guid EntityId { get; }

    /// <summary>
    /// Gets detached replacement node state for add/update operations.
    /// </summary>
    public GraphWorldNodeState? NodeState { get; }

    /// <summary>
    /// Gets detached replacement edge state for add/update operations.
    /// </summary>
    public GraphWorldEdgeState? EdgeState { get; }

    /// <summary>
    /// Gets the scenario business-valid instant after applying the mutation.
    /// </summary>
    public DateTimeOffset ValidAt { get; }

    /// <summary>
    /// Gets the scenario recorded instant after applying the mutation.
    /// </summary>
    public DateTimeOffset RecordedAt { get; }

    /// <summary>
    /// Gets the caller-provided mutation explanation.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Copies this detached mutation to another valid and recorded time.
    /// </summary>
    /// <param name="validAt">New business-valid instant.</param>
    /// <param name="recordedAt">New recorded instant.</param>
    /// <returns>A detached mutation with unchanged entity content and description.</returns>
    public GraphScenarioMutation At(DateTimeOffset validAt, DateTimeOffset recordedAt) =>
        new(
            new GraphScenarioMutationParameters
            {
                MutationKind = MutationKind,
                EntityKind = EntityKind,
                EntityId = EntityId,
                NodeState = NodeState,
                EdgeState = EdgeState,
                ValidAt = validAt,
                RecordedAt = recordedAt,
                Description = Description
            });

    /// <summary>
    /// Creates a detached node add mutation.
    /// </summary>
    public static GraphScenarioMutation AddNode(Node node, DateTimeOffset validAt, DateTimeOffset recordedAt, string description = "Add node") =>
        ForNode(GraphScenarioMutationKind.Add, node, validAt, recordedAt, description);

    /// <summary>
    /// Creates a detached node update mutation.
    /// </summary>
    public static GraphScenarioMutation UpdateNode(Node node, DateTimeOffset validAt, DateTimeOffset recordedAt, string description = "Update node") =>
        ForNode(GraphScenarioMutationKind.Update, node, validAt, recordedAt, description);

    /// <summary>
    /// Creates a node remove mutation.
    /// </summary>
    public static GraphScenarioMutation RemoveNode(Guid nodeId, DateTimeOffset validAt, DateTimeOffset recordedAt, string description = "Remove node") =>
        ForRemoval(GraphWorldEntityKind.Node, nodeId, validAt, recordedAt, description);

    /// <summary>
    /// Creates a detached edge add mutation.
    /// </summary>
    public static GraphScenarioMutation AddEdge(Edge edge, DateTimeOffset validAt, DateTimeOffset recordedAt, string description = "Add edge") =>
        ForEdge(GraphScenarioMutationKind.Add, edge, validAt, recordedAt, description);

    /// <summary>
    /// Creates a detached edge update mutation.
    /// </summary>
    public static GraphScenarioMutation UpdateEdge(Edge edge, DateTimeOffset validAt, DateTimeOffset recordedAt, string description = "Update edge") =>
        ForEdge(GraphScenarioMutationKind.Update, edge, validAt, recordedAt, description);

    /// <summary>
    /// Creates an edge remove mutation.
    /// </summary>
    public static GraphScenarioMutation RemoveEdge(Guid edgeId, DateTimeOffset validAt, DateTimeOffset recordedAt, string description = "Remove edge") =>
        ForRemoval(GraphWorldEntityKind.Edge, edgeId, validAt, recordedAt, description);

    private static GraphScenarioMutation ForNode(GraphScenarioMutationKind kind, Node node, DateTimeOffset validAt, DateTimeOffset recordedAt, string description)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new GraphScenarioMutation(
            new GraphScenarioMutationParameters
            {
                MutationKind = kind,
                EntityKind = GraphWorldEntityKind.Node,
                EntityId = node.Id,
                NodeState = new GraphWorldNodeState(node),
                EdgeState = null,
                ValidAt = validAt,
                RecordedAt = recordedAt,
                Description = ValidateDescription(description)
            });
    }

    private static GraphScenarioMutation ForEdge(GraphScenarioMutationKind kind, Edge edge, DateTimeOffset validAt, DateTimeOffset recordedAt, string description)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return new GraphScenarioMutation(
            new GraphScenarioMutationParameters
            {
                MutationKind = kind,
                EntityKind = GraphWorldEntityKind.Edge,
                EntityId = edge.Id,
                NodeState = null,
                EdgeState = new GraphWorldEdgeState(edge),
                ValidAt = validAt,
                RecordedAt = recordedAt,
                Description = ValidateDescription(description)
            });
    }

    private static GraphScenarioMutation ForRemoval(GraphWorldEntityKind kind, Guid id, DateTimeOffset validAt, DateTimeOffset recordedAt, string description)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A scenario mutation requires a non-empty entity identifier.", nameof(id));
        }

        return new GraphScenarioMutation(
            new GraphScenarioMutationParameters
            {
                MutationKind = GraphScenarioMutationKind.Remove,
                EntityKind = kind,
                EntityId = id,
                NodeState = null,
                EdgeState = null,
                ValidAt = validAt,
                RecordedAt = recordedAt,
                Description = ValidateDescription(description)
            });
    }

    private static string ValidateDescription(string description) =>
        string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("A scenario mutation description cannot be empty.", nameof(description))
            : description;

    private sealed class GraphScenarioMutationParameters
    {
        public required GraphScenarioMutationKind MutationKind { get; init; }

        public required GraphWorldEntityKind EntityKind { get; init; }

        public required Guid EntityId { get; init; }

        public required GraphWorldNodeState? NodeState { get; init; }

        public required GraphWorldEdgeState? EdgeState { get; init; }

        public required DateTimeOffset ValidAt { get; init; }

        public required DateTimeOffset RecordedAt { get; init; }

        public required string Description { get; init; }
    }
}