using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Defines a one-at-a-time parameter sensitivity experiment.
/// </summary>
public sealed class GraphSensitivityDefinition
{
    /// <summary>
    /// Gets the stable parameter name.
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// Gets the reference parameter value.
    /// </summary>
    public required double BaselineValue { get; init; }

    /// <summary>
    /// Gets the candidate parameter values.
    /// </summary>
    public required IReadOnlyCollection<double> Values { get; init; }

    /// <summary>
    /// Gets the pure finite scalar outcome selector.
    /// </summary>
    public required Func<GraphWorldSnapshot, double, double> OutcomeSelector { get; init; }

    /// <summary>
    /// Gets the maximum number of candidate values.
    /// </summary>
    public int MaximumValues { get; init; } = 10_000;
}