namespace Gorm.Application.Temporal.Scenarios;

/// <summary>
/// Represents a classified rejected scenario mutation.
/// </summary>
#pragma warning disable S3871
public sealed class GraphScenarioMutationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a scenario mutation failure.
    /// </summary>
    /// <param name="reason">Machine-readable reason.</param>
    /// <param name="message">Failure explanation.</param>
    public GraphScenarioMutationException(GraphScenarioFailureReason reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the machine-readable failure reason.
    /// </summary>
    public GraphScenarioFailureReason Reason { get; }
}
#pragma warning restore S3871