using System.Collections.ObjectModel;

namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Contains ordered rule matches for one drift report.
/// </summary>
public sealed class GraphTwinWarningEvaluation
{
    internal GraphTwinWarningEvaluation(int evaluatedRuleCount, GraphTwinWarning[] warnings)
    {
        EvaluatedRuleCount = evaluatedRuleCount;
        Warnings = Array.AsReadOnly(warnings);
        Explanation = $"Evaluated {evaluatedRuleCount} warning rules and matched {warnings.Length}.";
    }

    /// <summary>
    /// Gets the number of rules evaluated.
    /// </summary>
    public int EvaluatedRuleCount { get; }

    /// <summary>
    /// Gets matching warnings ordered by severity then stable id.
    /// </summary>
    public ReadOnlyCollection<GraphTwinWarning> Warnings { get; }

    /// <summary>
    /// Gets a deterministic summary.
    /// </summary>
    public string Explanation { get; }
}