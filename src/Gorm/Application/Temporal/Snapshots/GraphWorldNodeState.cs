using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Snapshots;

/// <summary>
/// Stores a detached, immutable serialized state of one graph node.
/// </summary>
public sealed class GraphWorldNodeState
{
    private readonly byte[] _payload;

    internal GraphWorldNodeState(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Id = node.Id;
        EntityType = node.GetType();
        _payload = GraphWorldStateSerializer.Serialize(node, EntityType);
    }

    /// <summary>
    /// Gets the node identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the concrete GORM node type captured by this state.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Creates a new detached node instance from the captured state.
    /// Changes to the returned instance never change this world state.
    /// </summary>
    public Node Materialize() =>
        (Node)GraphWorldStateSerializer.Deserialize(_payload, EntityType);

    /// <summary>
    /// Creates a new detached, strongly typed node instance from the captured state.
    /// </summary>
    /// <typeparam name="TNode">Expected concrete or base node type.</typeparam>
    /// <returns>A new detached node instance.</returns>
    public TNode Materialize<TNode>()
        where TNode : Node =>
        Materialize() as TNode ?? throw new InvalidOperationException(
            $"Captured node type '{EntityType.FullName}' is not assignable to '{typeof(TNode).FullName}'.");

    internal ReadOnlySpan<byte> Payload => _payload;
}