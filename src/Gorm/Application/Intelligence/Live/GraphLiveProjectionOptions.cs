namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Configures bounded live projection processing.
/// </summary>
public sealed class GraphLiveProjectionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of changes processed by one subscription.
    /// </summary>
    public int MaximumChanges { get; set; } = 1_000_000;
}