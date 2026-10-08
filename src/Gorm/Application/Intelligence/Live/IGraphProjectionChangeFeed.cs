using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Abstracts a provider-specific ordered source of graph projection deltas.
/// </summary>
public interface IGraphProjectionChangeFeed
{
    /// <summary>
    /// Reads changes for one explicit projection key strictly after an optional committed cursor.
    /// </summary>
    public IAsyncEnumerable<GraphProjectionChange> ReadChangesAsync(GraphProjectionKey key, GraphChangeFeedCursor? after = null, CancellationToken cancellationToken = default);
}