namespace Gorm.Application.Temporal.History;

/// <summary>
/// Controls bounded point-in-time projection from GORM history envelopes.
/// </summary>
public sealed class GraphWorldHistoryProjectionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of source envelopes that may be inspected.
    /// </summary>
    public int MaximumHistoryEntries { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets whether a selected deleted node is absent from the projected world.
    /// </summary>
    public bool DeletedNodesAreInactive { get; init; } = true;

    /// <summary>
    /// Gets or sets whether a selected deleted edge is absent from the projected world.
    /// </summary>
    public bool DeletedEdgesAreInactive { get; init; } = true;

    /// <summary>
    /// Gets or sets whether a selected disconnected edge is absent from the projected world.
    /// </summary>
    public bool DisconnectedEdgesAreInactive { get; init; } = true;
}