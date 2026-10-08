namespace Gorm.Application.Intelligence.Compute;

/// <summary>
/// Represents the final state of a vertex-centric graph computation.
/// </summary>
public sealed class GraphComputeResult<TState>
{
    internal GraphComputeResult(IReadOnlyDictionary<Guid, TState> states, int supersteps, long processedMessages, bool converged)
    {
        States = states;
        Supersteps = supersteps;
        ProcessedMessages = processedMessages;
        Converged = converged;
    }

    /// <summary>
    /// Gets final node states keyed by node identifier.
    /// </summary>
    public IReadOnlyDictionary<Guid, TState> States { get; }

    /// <summary>
    /// Gets the number of executed supersteps.
    /// </summary>
    public int Supersteps { get; }

    /// <summary>
    /// Gets the total number of messages consumed by nodes.
    /// </summary>
    public long ProcessedMessages { get; }

    /// <summary>
    /// Gets a value indicating whether execution became inactive before reaching its limit.
    /// </summary>
    public bool Converged { get; }

    /// <summary>
    /// Gets the final state for a node identifier.
    /// </summary>
    public TState GetState(Guid nodeId) =>
        States.TryGetValue(nodeId, out var state)
            ? state
            : throw new KeyNotFoundException($"Node '{nodeId}' has no compute state.");
}