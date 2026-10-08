using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Performs bounded one-at-a-time parameter sensitivity analysis.
/// </summary>
public static class GraphSensitivityAnalyzer
{
    /// <summary>
    /// Evaluates the reference and every unique candidate value without mutating the world.
    /// </summary>
    public static GraphSensitivityResult Analyze(GraphWorldSnapshot baseline, GraphSensitivityDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(definition);
        ValidateDefinition(definition);
        var values = definition.Values.Distinct().Order().ToArray();
        if (values.Length != definition.Values.Count)
        {
            throw new ArgumentException("Sensitivity values must be unique.", nameof(definition));
        }

        var baselineOutcome = Evaluate(baseline, definition.BaselineValue, definition.OutcomeSelector);
        var points = new GraphSensitivityPoint[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = values[index];
            var outcome = Evaluate(baseline, value, definition.OutcomeSelector);
            var relativeParameterChange = Math.Abs(definition.BaselineValue) < 1e-12 ? null : (double?)((value - definition.BaselineValue) / Math.Abs(definition.BaselineValue));
            var relativeOutcomeChange = Math.Abs(baselineOutcome) < 1e-12 ? null : (double?)((outcome - baselineOutcome) / Math.Abs(baselineOutcome));
            var elasticity = relativeParameterChange is not null && Math.Abs(relativeParameterChange.Value) >= 1e-12 && relativeOutcomeChange is not null
                ? relativeOutcomeChange / relativeParameterChange
                : null;
            points[index] = new GraphSensitivityPoint
            {
                ParameterValue = value,
                Outcome = outcome,
                OutcomeDelta = outcome - baselineOutcome,
                RelativeParameterChange = relativeParameterChange,
                RelativeOutcomeChange = relativeOutcomeChange,
                Elasticity = elasticity
            };
        }

        return new GraphSensitivityResult(
            definition.ParameterName.Trim(),
            definition.BaselineValue,
            baselineOutcome,
            points,
            Classify(points, baselineOutcome, definition.BaselineValue));
    }

    private static GraphSensitivityDirection Classify(IReadOnlyCollection<GraphSensitivityPoint> points, double baselineOutcome, double baselineValue)
    {
        var ordered = points
            .Select(point => (point.ParameterValue, point.Outcome))
            .Append((ParameterValue: baselineValue, Outcome: baselineOutcome))
            .GroupBy(point => point.ParameterValue)
            .Select(group => group.First())
            .OrderBy(point => point.ParameterValue)
            .ToArray();
        var increased = false;
        var decreased = false;
        for (var index = 1; index < ordered.Length; index++)
        {
            increased |= ordered[index].Outcome > ordered[index - 1].Outcome;
            decreased |= ordered[index].Outcome < ordered[index - 1].Outcome;
        }

        return (increased, decreased) switch
        {
            (false, false) => GraphSensitivityDirection.Flat,
            (true, false) => GraphSensitivityDirection.Increasing,
            (false, true) => GraphSensitivityDirection.Decreasing,
            _ => GraphSensitivityDirection.Mixed
        };
    }

    private static double Evaluate(GraphWorldSnapshot baseline, double value, Func<GraphWorldSnapshot, double, double> selector)
    {
        var outcome = selector(baseline, value);
        if (!double.IsFinite(outcome))
        {
            throw new InvalidOperationException($"Sensitivity outcome for value '{value}' must be finite.");
        }

        return outcome;
    }

    private static void ValidateDefinition(GraphSensitivityDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ParameterName))
        {
            throw new ArgumentException("ParameterName is required.", nameof(definition));
        }

        ArgumentNullException.ThrowIfNull(definition.Values);
        ArgumentNullException.ThrowIfNull(definition.OutcomeSelector);
        if (!double.IsFinite(definition.BaselineValue))
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "BaselineValue must be finite.");
        }

        if (definition.MaximumValues <= 0 || definition.Values.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Sensitivity value counts must be positive.");
        }

        if (definition.Values.Count > definition.MaximumValues)
        {
            throw new InvalidOperationException($"Sensitivity values exceed MaximumValues ({definition.MaximumValues}).");
        }

        if (definition.Values.Any(value => !double.IsFinite(value)))
        {
            throw new ArgumentException("Sensitivity values must be finite.", nameof(definition));
        }
    }
}