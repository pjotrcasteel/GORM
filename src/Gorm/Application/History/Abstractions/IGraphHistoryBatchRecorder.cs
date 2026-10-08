using System.Data.Common;
using Gorm.Application.History.Envelopes;

namespace Gorm.Application.History.Abstractions;

/// <summary>
/// Persists pre-collected history envelopes, optionally in an active database transaction.
/// </summary>
public interface IGraphHistoryBatchRecorder : IGraphHistoryRecorder
{
    /// <summary>
    /// Persists history envelopes using the supplied transaction when one is available.
    /// </summary>
    /// <param name="envelopes">The history envelopes to persist.</param>
    /// <param name="connection">The active connection, if any.</param>
    /// <param name="transaction">The active transaction, if any.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PersistAsync(
        IReadOnlyList<GraphHistoryEnvelope> envelopes,
        DbConnection? connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken);
}