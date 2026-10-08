namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Represents a classified event-stream integrity or replay failure.
/// </summary>
#pragma warning disable S3871
public sealed class GraphScenarioReplayException : InvalidOperationException
{
    /// <summary>
    /// Initializes a replay failure.
    /// </summary>
    /// <param name="reason">Machine-readable reason.</param>
    /// <param name="message">Failure explanation.</param>
    public GraphScenarioReplayException(GraphScenarioReplayFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the machine-readable failure reason.
    /// </summary>
    public GraphScenarioReplayFailureReason Reason { get; }
}
#pragma warning restore S3871