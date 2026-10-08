using System.Collections.ObjectModel;

namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Contains one-at-a-time sensitivity evidence and its qualitative direction.
/// </summary>
public sealed class GraphSensitivityResult
{
    internal GraphSensitivityResult(string parameterName, double baselineValue, double baselineOutcome, GraphSensitivityPoint[] points, GraphSensitivityDirection direction)
    {
        ParameterName = parameterName;
        BaselineValue = baselineValue;
        BaselineOutcome = baselineOutcome;
        Points = Array.AsReadOnly(points);
        Direction = direction;
        OutcomeRange = points.Select(point => point.Outcome).Append(baselineOutcome).Max() - points.Select(point => point.Outcome).Append(baselineOutcome).Min();
        MostImpactfulPoint = points.OrderByDescending(point => Math.Abs(point.OutcomeDelta)).ThenBy(point => point.ParameterValue).First();
        Explanation =
            $"Parameter '{parameterName}' is {direction.ToString().ToLowerInvariant()} across {points.Length} sampled values; " +
            $"the observed outcome range is {OutcomeRange:G17}.";
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the reference parameter value.
    /// </summary>
    public double BaselineValue { get; }

    /// <summary>
    /// Gets the reference outcome.
    /// </summary>
    public double BaselineOutcome { get; }

    /// <summary>
    /// Gets sensitivity points ordered by parameter value.
    /// </summary>
    public ReadOnlyCollection<GraphSensitivityPoint> Points { get; }

    /// <summary>
    /// Gets the sampled response direction.
    /// </summary>
    public GraphSensitivityDirection Direction { get; }

    /// <summary>
    /// Gets maximum minus minimum sampled/reference outcome.
    /// </summary>
    public double OutcomeRange { get; }

    /// <summary>
    /// Gets the point with the largest absolute outcome change.
    /// </summary>
    public GraphSensitivityPoint MostImpactfulPoint { get; }

    /// <summary>
    /// Gets a deterministic summary.
    /// </summary>
    public string Explanation { get; }
}