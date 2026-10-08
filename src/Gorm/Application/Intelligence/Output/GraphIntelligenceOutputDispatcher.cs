using System.Diagnostics;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Registry;
using Gorm.Application.Intelligence.Runtime;

namespace Gorm.Application.Intelligence.Output;

/// <summary>
/// Dispatches results only when the application explicitly calls <see cref="DispatchAsync"/>.
/// </summary>
public sealed class GraphIntelligenceOutputDispatcher
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a deterministic adapter pipeline.
    /// </summary>
    public GraphIntelligenceOutputDispatcher(IEnumerable<IGraphIntelligenceOutputAdapter> adapters, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        var materialized = adapters.ToArray();
        if (materialized.Any(adapter => adapter is null))
        {
            throw new ArgumentException("An output adapter cannot be null.", nameof(adapters));
        }

        var duplicate = materialized.GroupBy(adapter => adapter.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Output adapter '{duplicate.Key}' is registered more than once.", nameof(adapters));
        }

        if (materialized.Any(adapter => string.IsNullOrWhiteSpace(adapter.Id)))
        {
            throw new ArgumentException("An output adapter identifier cannot be empty.", nameof(adapters));
        }

        Adapters = Array.AsReadOnly(materialized);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets adapters in deterministic dispatch order.
    /// </summary>
    public IReadOnlyList<IGraphIntelligenceOutputAdapter> Adapters { get; }

    /// <summary>
    /// Explicitly dispatches one execution result to every configured adapter in registration order.
    /// </summary>
    public Task<IReadOnlyList<GraphOutputDispatchReceipt>> DispatchAsync(GraphAlgorithmExecutionResult execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return DispatchCoreAsync(execution, cancellationToken);
    }

    private async Task<IReadOnlyList<GraphOutputDispatchReceipt>> DispatchCoreAsync(GraphAlgorithmExecutionResult execution, CancellationToken cancellationToken)
    {
        var receipts = new List<GraphOutputDispatchReceipt>(Adapters.Count);
        foreach (var adapter in Adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = _timeProvider.GetUtcNow();
            var startedTimestamp = _timeProvider.GetTimestamp();
            var diagnosticTimestamp = Stopwatch.GetTimestamp();
            using var activity = GormIntelligenceDiagnostics.StartOutputActivity(adapter.Id, execution.AlgorithmId);
            try
            {
                await adapter.WriteAsync(execution, cancellationToken).ConfigureAwait(false);
                var completedAt = _timeProvider.GetUtcNow();
                receipts.Add(new GraphOutputDispatchReceipt
                {
                    AdapterId = adapter.Id,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    Duration = _timeProvider.GetElapsedTime(startedTimestamp)
                });
                GormIntelligenceDiagnostics.RecordSuccess(activity, diagnosticTimestamp, GormIntelligenceDiagnostics.ActivityNames.Output, mode: "explicit");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var wrapped = new GraphIntelligenceRuntimeException(
                    GraphIntelligenceFailureReason.OutputAdapterFailed,
                    $"Explicit output adapter '{adapter.Id}' failed for algorithm '{execution.AlgorithmId}'.",
                    exception,
                    execution.AlgorithmId,
                    execution.Snapshot,
                    adapter.Id);
                GormIntelligenceDiagnostics.RecordFailure(activity, diagnosticTimestamp, GormIntelligenceDiagnostics.ActivityNames.Output, wrapped);
                throw wrapped;
            }
        }

        return receipts.AsReadOnly();
    }
}