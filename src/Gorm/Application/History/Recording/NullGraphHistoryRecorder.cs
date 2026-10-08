using Gorm.Application.Context;
using Gorm.Application.History.Abstractions;
using Gorm.Application.Tracking;

namespace Gorm.Application.History.Recording;

/// <summary>
/// Represents a no-op history recorder.
/// </summary>
public sealed class NullGraphHistoryRecorder : IGraphHistoryRecorder
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static NullGraphHistoryRecorder Instance { get; } = new();

    private NullGraphHistoryRecorder()
    {
    }

    /// <summary>
    /// Captures history for the pending graph changes.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="capturedAtUtc">The capture timestamp in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CaptureAsync(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);

        return Task.CompletedTask;
    }
}