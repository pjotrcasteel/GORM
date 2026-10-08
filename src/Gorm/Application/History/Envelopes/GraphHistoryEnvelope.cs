using Gorm.Core.Primitives;

namespace Gorm.Application.History.Envelopes;

/// <summary>
/// Represents a captured history record for a node or edge.
/// </summary>
public sealed class GraphHistoryEnvelope
{
    /// <summary>
    /// Gets or sets the CLR type of the entity.
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// Gets or sets the entity identifier.
    /// </summary>
    public required Guid EntityId { get; init; }

    /// <summary>
    /// Gets or sets the history operation kind.
    /// </summary>
    public required GraphHistoryOperationKind OperationKind { get; init; }

    /// <summary>
    /// Gets or sets the captured at UTC timestamp.
    /// </summary>
    public required DateTime CapturedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the valid from UTC timestamp.
    /// </summary>
    public required DateTime ValidFromUtc { get; init; }

    /// <summary>
    /// Gets or sets the valid to UTC timestamp.
    /// </summary>
    public DateTime? ValidToUtc { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the record represents an edge.
    /// </summary>
    public required bool IsEdge { get; init; }

    /// <summary>
    /// Gets or sets the source node identifier for edge records.
    /// </summary>
    public Guid? FromId { get; init; }

    /// <summary>
    /// Gets or sets the target node identifier for edge records.
    /// </summary>
    public Guid? ToId { get; init; }

    /// <summary>
    /// Gets or sets the captured entity snapshot.
    /// </summary>
    public required object Snapshot { get; init; }

    /// <summary>
    /// Gets or sets the queried as-of UTC instant used to produce this envelope.
    /// This is query-time metadata and is not part of the persisted history record itself.
    /// </summary>
    public DateTime? QueryAsOfUtc { get; init; }

    /// <summary>
    /// Creates an envelope for the provided node snapshot.
    /// </summary>
    /// <param name="node">The node snapshot.</param>
    /// <param name="operationKind">The operation kind.</param>
    /// <param name="capturedAtUtc">The captured at UTC timestamp.</param>
    /// <param name="validFromUtc">The valid from UTC timestamp.</param>
    /// <param name="validToUtc">The valid to UTC timestamp.</param>
    /// <returns>The created envelope.</returns>
    public static GraphHistoryEnvelope ForNode(Node node, GraphHistoryOperationKind operationKind, DateTime capturedAtUtc, DateTime validFromUtc, DateTime? validToUtc = null)
    {
        ArgumentNullException.ThrowIfNull(node);

        return new GraphHistoryEnvelope
        {
            EntityType = node.GetType(),
            EntityId = node.Id,
            OperationKind = operationKind,
            CapturedAtUtc = capturedAtUtc,
            ValidFromUtc = validFromUtc,
            ValidToUtc = validToUtc,
            IsEdge = false,
            FromId = null,
            ToId = null,
            Snapshot = node
        };
    }

    /// <summary>
    /// Creates an envelope for the provided edge snapshot.
    /// </summary>
    /// <param name="edge">The edge snapshot.</param>
    /// <param name="operationKind">The operation kind.</param>
    /// <param name="capturedAtUtc">The captured at UTC timestamp.</param>
    /// <param name="validFromUtc">The valid from UTC timestamp.</param>
    /// <param name="validToUtc">The valid to UTC timestamp.</param>
    /// <returns>The created envelope.</returns>
    public static GraphHistoryEnvelope ForEdge(Edge edge, GraphHistoryOperationKind operationKind, DateTime capturedAtUtc, DateTime validFromUtc, DateTime? validToUtc = null)
    {
        ArgumentNullException.ThrowIfNull(edge);

        return new GraphHistoryEnvelope
        {
            EntityType = edge.GetType(),
            EntityId = edge.Id,
            OperationKind = operationKind,
            CapturedAtUtc = capturedAtUtc,
            ValidFromUtc = validFromUtc,
            ValidToUtc = validToUtc,
            IsEdge = true,
            FromId = edge.FromId,
            ToId = edge.ToId,
            Snapshot = edge
        };
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public T GetSnapshotOfType<T>() where T : class =>
        Snapshot as T ?? throw new InvalidOperationException(
            $"History snapshot is not of type '{typeof(T).FullName}'.");
}