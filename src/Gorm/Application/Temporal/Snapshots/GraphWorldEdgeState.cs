using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Snapshots;

/// <summary>
/// Stores a detached, immutable serialized state of one graph edge.
/// </summary>
public sealed class GraphWorldEdgeState
{
    private readonly byte[] _payload;

    internal GraphWorldEdgeState(Edge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        Id = edge.Id;
        FromId = edge.FromId;
        ToId = edge.ToId;
        EntityType = edge.GetType();
        _payload = GraphWorldStateSerializer.Serialize(edge, EntityType);
    }

    /// <summary>
    /// Gets the edge identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the source node identifier.
    /// </summary>
    public Guid FromId { get; }

    /// <summary>
    /// Gets the destination node identifier.
    /// </summary>
    public Guid ToId { get; }

    /// <summary>
    /// Gets the concrete GORM edge type captured by this state.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Creates a new detached edge instance from the captured state.
    /// Changes to the returned instance never change this world state.
    /// </summary>
    public Edge Materialize() =>
        (Edge)GraphWorldStateSerializer.Deserialize(_payload, EntityType);

    /// <summary>
    /// Creates a new detached, strongly typed edge instance from the captured state.
    /// </summary>
    /// <typeparam name="TEdge">Expected concrete or base edge type.</typeparam>
    /// <returns>A new detached edge instance.</returns>
    public TEdge Materialize<TEdge>()
        where TEdge : Edge =>
        Materialize() as TEdge ?? throw new InvalidOperationException(
            $"Captured edge type '{EntityType.FullName}' is not assignable to '{typeof(TEdge).FullName}'.");

    internal ReadOnlySpan<byte> Payload => _payload;
}