namespace Gorm.Application.History.Configuration;

/// <summary>
/// Represents options for graph history semantics.
/// </summary>
public sealed class GraphHistoryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the latest captured history record wins during temporal resolution.
    /// </summary>
    public bool LatestCapturedRecordWins { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether disconnected edges are terminal inactive states.
    /// </summary>
    public bool DisconnectedEdgesAreInactive { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether deleted edges are terminal inactive states.
    /// </summary>
    public bool DeletedEdgesAreInactive { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether deleted nodes are terminal inactive states.
    /// </summary>
    public bool DeletedNodesAreInactive { get; init; } = true;
}