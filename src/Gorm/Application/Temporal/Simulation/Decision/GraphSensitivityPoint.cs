namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Contains parameter, outcome and normalized sensitivity evidence at one point.
/// </summary>
public sealed class GraphSensitivityPoint
{
    /// <summary>
    /// Gets the evaluated parameter value.
    /// </summary>
    public required double ParameterValue { get; init; }

    /// <summary>
    /// Gets the finite model outcome.
    /// </summary>
    public required double Outcome { get; init; }

    /// <summary>
    /// Gets outcome minus baseline outcome.
    /// </summary>
    public required double OutcomeDelta { get; init; }

    /// <summary>
    /// Gets the relative parameter change, or null when the baseline parameter is zero.
    /// </summary>
    public double? RelativeParameterChange { get; init; }

    /// <summary>
    /// Gets the relative outcome change, or null when the baseline outcome is zero.
    /// </summary>
    public double? RelativeOutcomeChange { get; init; }

    /// <summary>
    /// Gets relative outcome change divided by relative parameter change when defined.
    /// </summary>
    public double? Elasticity { get; init; }
}