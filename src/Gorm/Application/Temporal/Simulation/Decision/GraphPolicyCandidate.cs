using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Defines one strategy and its pure application-owned metric evaluator.
/// </summary>
public sealed class GraphPolicyCandidate
{
    /// <summary>
    /// Gets the stable candidate identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets an optional human-readable description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the evaluator, which may create isolated scenarios but must return finite metrics.
    /// </summary>
    public required Func<GraphWorldSnapshot, IReadOnlyDictionary<string, double>> Evaluate { get; init; }
}