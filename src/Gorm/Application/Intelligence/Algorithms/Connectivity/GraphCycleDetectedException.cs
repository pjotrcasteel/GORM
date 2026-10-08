namespace Gorm.Application.Intelligence.Algorithms.Connectivity;

/// <summary>
/// The exception thrown when an algorithm requiring a directed acyclic graph encounters a cycle.
/// </summary>
public sealed class GraphCycleDetectedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphCycleDetectedException"/> class.
    /// </summary>
    public GraphCycleDetectedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphCycleDetectedException"/> class.
    /// </summary>
    public GraphCycleDetectedException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphCycleDetectedException"/> class.
    /// </summary>
    public GraphCycleDetectedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    internal GraphCycleDetectedException(Guid[] remainingNodeIds)
        : base(
            "Critical-path analysis requires a directed acyclic graph. " +
            $"A cycle involves {remainingNodeIds.Length} unorderable node(s): {string.Join(", ", remainingNodeIds)}.")
    {
        RemainingNodeIds = [.. remainingNodeIds];
    }

    /// <summary>
    /// Gets nodes that could not be topologically ordered because they belong to, or depend on, a cycle.
    /// </summary>
    public Guid[] RemainingNodeIds { get; } = [];
}