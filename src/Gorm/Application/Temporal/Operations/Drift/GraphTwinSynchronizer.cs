using System.Runtime.CompilerServices;

namespace Gorm.Application.Temporal.Operations.Drift;

/// <summary>
/// Converts an ordered provider-neutral observation stream into bounded drift results.
/// </summary>
public static class GraphTwinSynchronizer
{
    /// <summary>
    /// Watches expected/actual world pairs without owning a provider or persisting state.
    /// </summary>
    public static IAsyncEnumerable<GraphTwinSynchronizationResult> WatchAsync(
        IAsyncEnumerable<GraphTwinObservation> observations,
        GraphTwinDriftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);
        options ??= new GraphTwinDriftOptions();
        GraphTwinDriftDetector.ValidateOptions(options);
        return WatchCoreAsync(observations, options, cancellationToken);
    }

    private static async IAsyncEnumerable<GraphTwinSynchronizationResult> WatchCoreAsync(
        IAsyncEnumerable<GraphTwinObservation> observations,
        GraphTwinDriftOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var count = 0;
        var previousSequence = -1L;
        DateTimeOffset? previousObservedAt = null;
        await foreach (var observation in observations.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(observation);
            ArgumentNullException.ThrowIfNull(observation.Expected);
            ArgumentNullException.ThrowIfNull(observation.Actual);
            if (++count > options.MaximumObservations)
            {
                throw new InvalidOperationException(
                    $"Twin synchronization exceeded MaximumObservations ({options.MaximumObservations}).");
            }

            if (observation.Sequence < 0 || observation.Sequence <= previousSequence)
            {
                throw new InvalidOperationException("Twin observation sequences must be non-negative and strictly increasing.");
            }

            var observedAt = observation.ObservedAt.ToUniversalTime();
            if (previousObservedAt is not null && observedAt < previousObservedAt)
            {
                throw new InvalidOperationException("Twin observation time may not move backwards.");
            }

            previousSequence = observation.Sequence;
            previousObservedAt = observedAt;
            yield return new GraphTwinSynchronizationResult
            {
                Sequence = observation.Sequence,
                ObservedAt = observedAt,
                Drift = GraphTwinDriftDetector.Compare(observation.Expected, observation.Actual, options, cancellationToken)
            };
        }
    }
}