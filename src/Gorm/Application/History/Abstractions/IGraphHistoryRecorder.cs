using Gorm.Application.Context;
using Gorm.Application.Tracking;

namespace Gorm.Application.History.Abstractions;

/// <summary>
/// Captures graph history for pending changes.
/// </summary>
public interface IGraphHistoryRecorder
{
    /// <summary>
    /// Captures history for the pending graph changes.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="capturedAtUtc">The capture timestamp in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CaptureAsync(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc, CancellationToken cancellationToken);
}