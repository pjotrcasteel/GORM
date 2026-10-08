using Gorm.Application.Temporal.Operations.Drift;

namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Evaluates bounded transparent threshold rules over twin drift evidence.
/// </summary>
public static class GraphTwinWarningEngine
{
    /// <summary>
    /// Evaluates every unique rule and returns matching evidence without dispatching it.
    /// </summary>
    public static GraphTwinWarningEvaluation Evaluate(
        GraphTwinDriftReport drift,
        IReadOnlyCollection<GraphTwinWarningRule> rules,
        GraphTwinWarningOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drift);
        ArgumentNullException.ThrowIfNull(rules);
        options ??= new GraphTwinWarningOptions();
        Validate(rules, options);
        var warnings = new List<GraphTwinWarning>();
        foreach (var rule in rules.OrderBy(rule => rule.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metric = rule.MetricSelector(drift);
            if (!double.IsFinite(metric))
            {
                throw new InvalidOperationException($"Warning rule '{rule.Id}' returned a non-finite metric.");
            }

            if (!Matches(metric, rule.Threshold, rule.Comparison))
            {
                continue;
            }

            if (warnings.Count == options.MaximumWarnings)
            {
                throw new InvalidOperationException(
                    $"Warning evaluation exceeded MaximumWarnings ({options.MaximumWarnings}).");
            }

            warnings.Add(new GraphTwinWarning
            {
                RuleId = rule.Id,
                Severity = rule.Severity,
                MetricValue = metric,
                Threshold = rule.Threshold,
                Comparison = rule.Comparison,
                Explanation = $"Rule '{rule.Id}' matched: metric {metric:G17} {Symbol(rule.Comparison)} {rule.Threshold:G17}."
            });
        }

        return new GraphTwinWarningEvaluation(
            rules.Count,
            [.. warnings.OrderByDescending(warning => warning.Severity).ThenBy(warning => warning.RuleId, StringComparer.Ordinal)]);
    }

    private static bool Matches(double value, double threshold, GraphThresholdComparison comparison) => comparison switch
    {
        GraphThresholdComparison.GreaterThan => value > threshold,
        GraphThresholdComparison.GreaterThanOrEqual => value >= threshold,
        GraphThresholdComparison.LessThan => value < threshold,
        GraphThresholdComparison.LessThanOrEqual => value <= threshold,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison))
    };

    private static string Symbol(GraphThresholdComparison comparison) => comparison switch
    {
        GraphThresholdComparison.GreaterThan => ">",
        GraphThresholdComparison.GreaterThanOrEqual => ">=",
        GraphThresholdComparison.LessThan => "<",
        GraphThresholdComparison.LessThanOrEqual => "<=",
        _ => throw new ArgumentOutOfRangeException(nameof(comparison))
    };

    private static void Validate(IReadOnlyCollection<GraphTwinWarningRule> rules, GraphTwinWarningOptions options)
    {
        if (options.MaximumRules <= 0 || options.MaximumWarnings <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (rules.Count > options.MaximumRules)
        {
            throw new InvalidOperationException($"Warning rules exceed MaximumRules ({options.MaximumRules}).");
        }

        if (rules.Any(rule => string.IsNullOrWhiteSpace(rule.Id) || rule.MetricSelector is null || !double.IsFinite(rule.Threshold)) ||
            rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count() != rules.Count)
        {
            throw new ArgumentException("Warning rule ids must be unique and metrics/thresholds must be valid.", nameof(rules));
        }
    }
}