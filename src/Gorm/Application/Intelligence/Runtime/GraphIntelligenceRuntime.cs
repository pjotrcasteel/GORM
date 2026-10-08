using System.Diagnostics;
using System.Runtime.CompilerServices;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Live;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Runtime;

/// <summary>
/// Unifies bounded one-shot and live algorithm execution without performing implicit output or persistence.
/// </summary>
public sealed class GraphIntelligenceRuntime
{
    private readonly GraphAlgorithmRegistry _registry;
    private readonly GraphLiveProjectionEngine _liveProjection;
    private readonly GraphIntelligenceRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes the runtime.
    /// </summary>
    public GraphIntelligenceRuntime(
        GraphAlgorithmRegistry registry,
        GraphProjectionCache projectionCache,
        GraphIntelligenceRuntimeOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(projectionCache);
        var configuredOptions = options ?? new GraphIntelligenceRuntimeOptions();
        _options = new GraphIntelligenceRuntimeOptions
        {
            ExecutionTimeout = configuredOptions.ExecutionTimeout,
            MaximumLiveExecutions = configuredOptions.MaximumLiveExecutions
        };
        _options.LiveProjection.MaximumChanges = configuredOptions.LiveProjection.MaximumChanges;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (_options.ExecutionTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Execution timeout must be positive or null.");
        }

        if (_options.MaximumLiveExecutions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum live executions must be greater than zero.");
        }

        _liveProjection = new GraphLiveProjectionEngine(
            projectionCache,
            new GraphLiveProjectionOptions
            {
                MaximumChanges = Math.Min(_options.LiveProjection.MaximumChanges, _options.MaximumLiveExecutions)
            },
            _timeProvider);
    }

    /// <summary>
    /// Executes one validated registry invocation under the shared timeout and diagnostics model.
    /// </summary>
    public GraphAlgorithmExecutionResult Execute(GraphProjectionSnapshot snapshot, GraphAlgorithmInvocation invocation, CancellationToken cancellationToken = default) =>
        ExecuteCore(snapshot, invocation, "one-shot", cancellationToken);

    private GraphAlgorithmExecutionResult ExecuteCore(GraphProjectionSnapshot snapshot, GraphAlgorithmInvocation invocation, string mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = GormIntelligenceDiagnostics.StartRuntimeActivity(invocation.AlgorithmId, snapshot, mode);
        using var timeout = CreateExecutionCancellation(cancellationToken);

        try
        {
            var result = _registry.Execute(snapshot, invocation, timeout?.Token ?? cancellationToken);
            GormIntelligenceDiagnostics.RecordSuccess(
                activity,
                startedAt,
                GormIntelligenceDiagnostics.ActivityNames.Runtime,
                snapshot.Projection.Statistics.NodeCount,
                snapshot.Projection.Statistics.EdgeCount,
                mode);
            return result;
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested && timeout?.IsCancellationRequested == true)
        {
            var wrapped = new GraphIntelligenceRuntimeException(
                GraphIntelligenceFailureReason.TimedOut,
                $"Algorithm '{invocation.AlgorithmId}' exceeded the configured timeout '{_options.ExecutionTimeout}'.",
                exception,
                invocation.AlgorithmId,
                snapshot.Metadata);
            GormIntelligenceDiagnostics.RecordFailure(activity, startedAt, GormIntelligenceDiagnostics.ActivityNames.Runtime, wrapped);
            throw wrapped;
        }
        catch (OperationCanceledException exception)
        {
            activity?.SetTag(GormIntelligenceDiagnostics.TagNames.Mode, "cancelled");
            GormIntelligenceDiagnostics.RecordFailure(activity, startedAt, GormIntelligenceDiagnostics.ActivityNames.Runtime, exception);
            throw;
        }
        catch (GraphIntelligenceRuntimeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = exception is ArgumentException or KeyNotFoundException ? GraphIntelligenceFailureReason.InvalidInvocation : GraphIntelligenceFailureReason.AlgorithmFailed;
            var wrapped = new GraphIntelligenceRuntimeException(
                reason,
                $"Algorithm '{invocation.AlgorithmId}' failed for projection '{snapshot.Key}' version {snapshot.Version}.",
                exception,
                invocation.AlgorithmId,
                snapshot.Metadata);
            GormIntelligenceDiagnostics.RecordFailure(activity, startedAt, GormIntelligenceDiagnostics.ActivityNames.Runtime, wrapped);
            throw wrapped;
        }
    }

    /// <summary>
    /// Applies live projection updates and yields one bounded algorithm result per committed change.
    /// </summary>
    public IAsyncEnumerable<GraphLiveAlgorithmUpdate> WatchAsync(
        GraphProjectionKey key,
        IGraphProjectionChangeFeed changeFeed,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        Func<GraphProjectionSnapshot, GraphAlgorithmInvocation> invocationFactory,
        GraphChangeFeedCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(changeFeed);
        ArgumentNullException.ThrowIfNull(rebuildFactory);
        ArgumentNullException.ThrowIfNull(invocationFactory);
        return WatchCoreAsync(key, changeFeed, rebuildFactory, invocationFactory, after, cancellationToken);
    }

    private async IAsyncEnumerable<GraphLiveAlgorithmUpdate> WatchCoreAsync(
        GraphProjectionKey key,
        IGraphProjectionChangeFeed changeFeed,
        Func<CancellationToken, Task<GraphProjectionSnapshot>> rebuildFactory,
        Func<GraphProjectionSnapshot, GraphAlgorithmInvocation> invocationFactory,
        GraphChangeFeedCursor? after,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var executions = 0;
        await using var enumerator = _liveProjection.WatchAsync(key, changeFeed, rebuildFactory, after, cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (await MoveNextAsync(enumerator, key, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (executions >= _options.MaximumLiveExecutions)
            {
                throw new GraphIntelligenceRuntimeException(
                    GraphIntelligenceFailureReason.LiveExecutionLimitExceeded,
                    $"Live projection '{key}' reached the maximum of {_options.MaximumLiveExecutions} algorithm executions.");
            }

            var projectionUpdate = enumerator.Current;
            GraphAlgorithmInvocation invocation;
            try
            {
                invocation = invocationFactory(projectionUpdate.ProjectionUpdate.Snapshot)
                    ?? throw new InvalidOperationException("The live invocation factory returned no invocation.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new GraphIntelligenceRuntimeException(
                    GraphIntelligenceFailureReason.InvalidInvocation,
                    $"The live invocation factory failed for projection '{key}' version " +
                    $"{projectionUpdate.ProjectionUpdate.Snapshot.Version}.",
                    exception,
                    snapshot: projectionUpdate.ProjectionUpdate.Snapshot.Metadata);
            }

            var result = ExecuteCore(projectionUpdate.ProjectionUpdate.Snapshot, invocation, "live", cancellationToken);
            executions++;
            yield return new GraphLiveAlgorithmUpdate
            {
                Cursor = projectionUpdate.Cursor,
                ProjectionUpdate = projectionUpdate.ProjectionUpdate,
                Execution = result,
                ProcessedAt = _timeProvider.GetUtcNow()
            };
        }
    }

    private static async ValueTask<bool> MoveNextAsync(IAsyncEnumerator<GraphLiveProjectionUpdate> enumerator, GraphProjectionKey key, CancellationToken cancellationToken)
    {
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (GraphLiveChangeLimitException exception)
        {
            throw new GraphIntelligenceRuntimeException(
                GraphIntelligenceFailureReason.LiveExecutionLimitExceeded,
                $"Live projection '{key}' reached its configured execution limit.",
                exception);
        }
        catch (Exception exception)
        {
            throw new GraphIntelligenceRuntimeException(GraphIntelligenceFailureReason.ChangeFeedFailed, $"Live projection feed '{key}' failed.", exception);
        }
    }

    private CancellationTokenSource? CreateExecutionCancellation(CancellationToken cancellationToken)
    {
        if (_options.ExecutionTimeout is not { } timeout)
        {
            return cancellationToken.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }
}