using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Verifies that a baseline and event stream reproduce a scenario checkpoint exactly.
/// </summary>
public static class GraphScenarioVerifier
{
    /// <summary>
    /// Replays and verifies a scenario checkpoint.
    /// </summary>
    /// <param name="checkpoint">Expected scenario identities.</param>
    /// <param name="baseline">Baseline to replay from.</param>
    /// <param name="eventStream">Integrity-checked event stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result and replay evidence.</returns>
    public static GraphScenarioVerificationResult Verify(
        GraphScenarioCheckpoint checkpoint,
        GraphWorldSnapshot baseline,
        GraphScenarioEventStream eventStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(eventStream);
        cancellationToken.ThrowIfCancellationRequested();

        if (checkpoint.ScenarioId != eventStream.ScenarioId)
        {
            return Invalid("Checkpoint scenario id does not match the event stream.");
        }

        if (!string.Equals(checkpoint.BaselineSnapshotId, baseline.Identity.SnapshotId, StringComparison.Ordinal))
        {
            return Invalid("Checkpoint baseline snapshot id does not match the supplied baseline.");
        }

        if (!string.Equals(checkpoint.StreamFingerprint, eventStream.StreamFingerprint, StringComparison.Ordinal))
        {
            return Invalid("Checkpoint stream fingerprint does not match the supplied event stream.");
        }

        var replay = eventStream.Replay(baseline, cancellationToken: cancellationToken);
        var valid = checkpoint.Revision == replay.Scenario.Revision &&
            string.Equals(checkpoint.FinalSnapshotId, replay.Scenario.Current.Identity.SnapshotId, StringComparison.Ordinal);
        return new GraphScenarioVerificationResult
        {
            IsValid = valid,
            Replay = replay,
            Explanation = valid
                ? $"Checkpoint reproduced scenario '{checkpoint.ScenarioId.Value}' revision {checkpoint.Revision} exactly."
                : "Replay completed but revision or final snapshot identity did not match the checkpoint."
        };
    }

    private static GraphScenarioVerificationResult Invalid(string explanation) =>
        new() { IsValid = false, Explanation = explanation };
}