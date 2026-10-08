using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Represents a validated deterministic hash chain of detached scenario mutations.
/// </summary>
public sealed class GraphScenarioEventStream
{
    private readonly GraphScenarioEvent[] _events;

    private GraphScenarioEventStream(GraphScenarioId scenarioId, GraphScenarioEvent[] events)
    {
        ScenarioId = scenarioId;
        _events = events;
        Events = Array.AsReadOnly(events);
        StreamFingerprint = events.Length == 0 ? EmptyStreamFingerprint(scenarioId) : events[^1].EventId;
    }

    /// <summary>
    /// Gets the scenario identifier bound into every event hash.
    /// </summary>
    public GraphScenarioId ScenarioId { get; }

    /// <summary>
    /// Gets validated events in sequence order.
    /// </summary>
    public ReadOnlyCollection<GraphScenarioEvent> Events { get; }

    /// <summary>
    /// Gets the final hash-chain identifier.
    /// </summary>
    public string StreamFingerprint { get; }

    /// <summary>
    /// Creates a deterministic event stream from detached mutations.
    /// </summary>
    /// <param name="scenarioId">Scenario identifier.</param>
    /// <param name="mutations">Mutations in intended replay order.</param>
    /// <param name="options">Event safety limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content-addressed event stream.</returns>
    public static GraphScenarioEventStream Create(
        GraphScenarioId scenarioId,
        IEnumerable<GraphScenarioMutation> mutations,
        GraphScenarioReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenarioId);
        ArgumentNullException.ThrowIfNull(mutations);
        options ??= new GraphScenarioReplayOptions();
        ValidateOptions(options);
        var events = new List<GraphScenarioEvent>();
        string? previousEventId = null;

        foreach (var mutation in mutations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(mutation);
            EnsureWithinLimit(events.Count, options.MaximumEvents);
            var sequence = events.Count + 1L;
            var eventId = GraphScenarioEventHasher.Calculate(scenarioId, sequence, previousEventId, mutation);
            events.Add(new GraphScenarioEvent
            {
                Sequence = sequence,
                PreviousEventId = previousEventId,
                EventId = eventId,
                Mutation = mutation
            });
            previousEventId = eventId;
        }

        return new GraphScenarioEventStream(scenarioId, [.. events]);
    }

    /// <summary>
    /// Restores and validates externally serialized event records.
    /// </summary>
    /// <param name="scenarioId">Scenario identifier expected by the event hashes.</param>
    /// <param name="events">Serialized event records.</param>
    /// <param name="options">Event safety limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated event stream.</returns>
    public static GraphScenarioEventStream Restore(
        GraphScenarioId scenarioId,
        IEnumerable<GraphScenarioEvent> events,
        GraphScenarioReplayOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenarioId);
        ArgumentNullException.ThrowIfNull(events);
        options ??= new GraphScenarioReplayOptions();
        ValidateOptions(options);
        var validated = new List<GraphScenarioEvent>();
        string? previousEventId = null;

        foreach (var scenarioEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(scenarioEvent);
            EnsureWithinLimit(validated.Count, options.MaximumEvents);
            var expectedSequence = validated.Count + 1L;
            if (scenarioEvent.Sequence != expectedSequence)
            {
                throw new GraphScenarioReplayException(
                    GraphScenarioReplayFailureReason.SequenceMismatch,
                    $"Expected event sequence {expectedSequence}, received {scenarioEvent.Sequence}.");
            }

            var expectedId = GraphScenarioEventHasher.Calculate(scenarioId, scenarioEvent.Sequence, previousEventId, scenarioEvent.Mutation);
            if (!string.Equals(scenarioEvent.PreviousEventId, previousEventId, StringComparison.Ordinal) ||
                !string.Equals(scenarioEvent.EventId, expectedId, StringComparison.Ordinal))
            {
                throw new GraphScenarioReplayException(
                    GraphScenarioReplayFailureReason.IntegrityMismatch,
                    $"Event {scenarioEvent.Sequence} does not match its deterministic hash chain.");
            }

            validated.Add(scenarioEvent);
            previousEventId = scenarioEvent.EventId;
        }

        return new GraphScenarioEventStream(scenarioId, [.. validated]);
    }

    /// <summary>
    /// Replays every event against a baseline and returns the deterministic final scenario.
    /// </summary>
    /// <param name="baseline">Unchanged scenario baseline.</param>
    /// <param name="options">Replay safety limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The replayed scenario and integrity evidence.</returns>
    public GraphScenarioReplayResult Replay(GraphWorldSnapshot baseline, GraphScenarioReplayOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        options ??= new GraphScenarioReplayOptions();
        ValidateOptions(options);
        if (_events.Length > options.MaximumEvents)
        {
            throw EventLimit(options.MaximumEvents);
        }

        var scenario = GraphScenario.Fork(baseline, ScenarioId);
        var previousRecordedAt = baseline.Identity.RecordedAt;
        foreach (var scenarioEvent in _events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scenarioEvent.Mutation.RecordedAt < previousRecordedAt)
            {
                throw new GraphScenarioReplayException(
                    GraphScenarioReplayFailureReason.TemporalOrderViolation,
                    $"Event {scenarioEvent.Sequence} recorded time moves backwards.");
            }

            scenario = scenario.Apply(scenarioEvent.Mutation, cancellationToken).Scenario;
            previousRecordedAt = scenarioEvent.Mutation.RecordedAt;
        }

        return new GraphScenarioReplayResult
        {
            Scenario = scenario,
            AppliedEventCount = _events.Length,
            StreamFingerprint = StreamFingerprint,
            Explanation =
                $"Replayed {_events.Length} integrity-checked events for scenario '{ScenarioId.Value}' to " +
                $"world version {scenario.Current.Identity.Version}."
        };
    }

    private static void ValidateOptions(GraphScenarioReplayOptions options)
    {
        if (options.MaximumEvents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumEvents must be positive.");
        }
    }

    private static void EnsureWithinLimit(int count, int maximumEvents)
    {
        if (count == maximumEvents)
        {
            throw EventLimit(maximumEvents);
        }
    }

    private static GraphScenarioReplayException EventLimit(int maximumEvents) =>
        new(
            GraphScenarioReplayFailureReason.EventLimitExceeded,
            $"Scenario stream exceeded MaximumEvents ({maximumEvents}).");

    private static string EmptyStreamFingerprint(GraphScenarioId scenarioId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"scenario:{scenarioId.Value}:empty")))
            .ToLowerInvariant();
}