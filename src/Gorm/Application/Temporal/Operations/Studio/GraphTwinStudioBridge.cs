using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Operations.Reporting;
using Gorm.Application.Temporal.Operations.Warnings;

namespace Gorm.Application.Temporal.Operations.Studio;

/// <summary>
/// Maps Twin evidence to native-desktop DTOs without referencing a UI framework or web host.
/// </summary>
public static class GraphTwinStudioBridge
{
    /// <summary>
    /// Creates one chronological desktop timeline row.
    /// </summary>
    public static GraphTwinStudioTimelineEntry CreateTimelineEntry(GraphTwinSynchronizationResult synchronization, GraphTwinWarningEvaluation? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(synchronization);
        var difference = synchronization.Drift.Difference;
        return new GraphTwinStudioTimelineEntry
        {
            Sequence = synchronization.Sequence,
            ObservedAt = synchronization.ObservedAt,
            Severity = synchronization.Drift.Severity,
            Added = difference.AddedCount,
            Removed = difference.RemovedCount,
            Modified = difference.ModifiedCount,
            WarningCount = warnings?.Warnings.Count ?? 0
        };
    }

    /// <summary>
    /// Creates fixed explorer panels over a verified evidence report.
    /// </summary>
    public static GraphTwinStudioExplorerModel CreateExplorerModel(GraphTwinEvidenceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Content);
        return new GraphTwinStudioExplorerModel
        {
            ReportId = report.Content.ReportId,
            ReportFingerprint = report.ReportFingerprint,
            Headline = report.Content.Summary,
            Panels =
            [
                new GraphTwinStudioPanel { Id = "changes", Title = "Observed drift", ItemCount = report.Content.Changes.Count },
                new GraphTwinStudioPanel { Id = "warnings", Title = "Warnings", ItemCount = report.Content.Warnings.Count },
                new GraphTwinStudioPanel { Id = "predictions", Title = "Predictions", ItemCount = report.Content.Predictions.Count },
                new GraphTwinStudioPanel { Id = "scenarios", Title = "Scenarios", ItemCount = report.Content.Scenarios.Count }
            ]
        };
    }
}