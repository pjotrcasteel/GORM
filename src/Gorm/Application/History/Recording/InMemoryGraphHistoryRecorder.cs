using System.Data.Common;
using Gorm.Application.Context;
using Gorm.Application.History.Abstractions;
using Gorm.Application.History.Envelopes;
using Gorm.Application.History.Storage;
using Gorm.Application.Tracking;

namespace Gorm.Application.History.Recording;

/// <summary>
/// Captures graph history into an in-memory history store.
/// </summary>
public sealed class InMemoryGraphHistoryRecorder : IGraphHistoryBatchRecorder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryGraphHistoryRecorder"/> class.
    /// </summary>
    /// <param name="historyStore">The history store.</param>
    public InMemoryGraphHistoryRecorder(InMemoryGraphHistoryStore historyStore)
    {
        HistoryStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
    }

    /// <summary>
    /// Gets the history store.
    /// </summary>
    public InMemoryGraphHistoryStore HistoryStore { get; }

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

        var entries = GraphHistoryEnvelopeCollector.Collect(context, changeTracker, capturedAtUtc);
        return PersistAsync(entries, connection: null, transaction: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task PersistAsync(
        IReadOnlyList<GraphHistoryEnvelope> envelopes,
        DbConnection? connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        cancellationToken.ThrowIfCancellationRequested();

        if (connection is not null || transaction is not null)
        {
            throw new InvalidOperationException("In-memory history cannot participate in a database transaction.");
        }

        HistoryStore.AddRange(envelopes);
        return Task.CompletedTask;
    }
}