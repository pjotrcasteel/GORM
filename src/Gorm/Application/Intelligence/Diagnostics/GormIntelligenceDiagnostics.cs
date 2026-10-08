using System.Diagnostics;
using System.Diagnostics.Metrics;
using Gorm.Application.Diagnostics;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Diagnostics;

/// <summary>
/// Emits graph intelligence telemetry through the existing GORM activity source and meter.
/// </summary>
public static class GormIntelligenceDiagnostics
{
    private static readonly Counter<long> OperationCounter =
        GormDiagnostics.Meter.CreateCounter<long>(MetricNames.OperationCount);
    private static readonly Histogram<double> OperationDuration =
        GormDiagnostics.Meter.CreateHistogram<double>(MetricNames.OperationDurationMilliseconds);

    /// <summary>
    /// Stable activity names emitted by graph intelligence.
    /// </summary>
    public static class ActivityNames
    {
        /// <summary>
        /// Gets the algorithm activity name.
        /// </summary>
        public const string Algorithm = "gorm.intelligence.algorithm";

        /// <summary>
        /// Gets the projection materialization activity name.
        /// </summary>
        public const string Projection = "gorm.intelligence.projection";

        /// <summary>
        /// Gets the unified runtime activity name.
        /// </summary>
        public const string Runtime = "gorm.intelligence.runtime";

        /// <summary>
        /// Gets the explicit output dispatch activity name.
        /// </summary>
        public const string Output = "gorm.intelligence.output";
    }

    /// <summary>
    /// Stable metric names emitted by graph intelligence.
    /// </summary>
    public static class MetricNames
    {
        /// <summary>
        /// Gets the completed operation counter name.
        /// </summary>
        public const string OperationCount = "gorm.intelligence.operation.count";

        /// <summary>
        /// Gets the operation duration histogram name.
        /// </summary>
        public const string OperationDurationMilliseconds = "gorm.intelligence.operation.duration.ms";
    }

    /// <summary>
    /// Stable tag names emitted by graph intelligence.
    /// </summary>
    public static class TagNames
    {
        /// <summary>
        /// Gets the operation tag.
        /// </summary>
        public const string Operation = "gorm.intelligence.operation";

        /// <summary>
        /// Gets the algorithm type tag.
        /// </summary>
        public const string Algorithm = "gorm.intelligence.algorithm.type";

        /// <summary>
        /// Gets the node-count tag.
        /// </summary>
        public const string NodeCount = "gorm.intelligence.node.count";

        /// <summary>
        /// Gets the edge-count tag.
        /// </summary>
        public const string EdgeCount = "gorm.intelligence.edge.count";

        /// <summary>
        /// Gets the projection-source count tag.
        /// </summary>
        public const string SourceCount = "gorm.intelligence.source.count";

        /// <summary>
        /// Gets the update/result mode tag.
        /// </summary>
        public const string Mode = "gorm.intelligence.mode";

        /// <summary>
        /// Gets the registry algorithm identifier tag.
        /// </summary>
        public const string AlgorithmId = "gorm.intelligence.algorithm.id";

        /// <summary>
        /// Gets the projection key tag.
        /// </summary>
        public const string ProjectionKey = "gorm.intelligence.projection.key";

        /// <summary>
        /// Gets the projection version tag.
        /// </summary>
        public const string ProjectionVersion = "gorm.intelligence.projection.version";

        /// <summary>
        /// Gets the explicit output adapter identifier tag.
        /// </summary>
        public const string OutputAdapter = "gorm.intelligence.output.adapter";
    }

    internal static Activity? StartAlgorithmActivity(Type algorithmType, GraphProjection projection)
    {
        var activity = GormDiagnostics.ActivitySource.StartActivity(ActivityNames.Algorithm, ActivityKind.Internal);
        activity?.SetTag(TagNames.Operation, ActivityNames.Algorithm);
        activity?.SetTag(TagNames.Algorithm, algorithmType.FullName);
        activity?.SetTag(TagNames.NodeCount, projection.Statistics.NodeCount);
        activity?.SetTag(TagNames.EdgeCount, projection.Statistics.EdgeCount);
        return activity;
    }

    internal static Activity? StartProjectionActivity(int sourceCount)
    {
        var activity = GormDiagnostics.ActivitySource.StartActivity(ActivityNames.Projection, ActivityKind.Internal);
        activity?.SetTag(TagNames.Operation, ActivityNames.Projection);
        activity?.SetTag(TagNames.SourceCount, sourceCount);
        return activity;
    }

    internal static Activity? StartRuntimeActivity(string algorithmId, GraphProjectionSnapshot snapshot, string mode)
    {
        var activity = GormDiagnostics.ActivitySource.StartActivity(ActivityNames.Runtime, ActivityKind.Internal);
        activity?.SetTag(TagNames.Operation, ActivityNames.Runtime);
        activity?.SetTag(TagNames.AlgorithmId, algorithmId);
        activity?.SetTag(TagNames.ProjectionKey, snapshot.Key.Value);
        activity?.SetTag(TagNames.ProjectionVersion, snapshot.Version);
        activity?.SetTag(TagNames.NodeCount, snapshot.Projection.Statistics.NodeCount);
        activity?.SetTag(TagNames.EdgeCount, snapshot.Projection.Statistics.EdgeCount);
        activity?.SetTag(TagNames.Mode, mode);
        return activity;
    }

    internal static Activity? StartOutputActivity(string adapterId, string algorithmId)
    {
        var activity = GormDiagnostics.ActivitySource.StartActivity(ActivityNames.Output, ActivityKind.Internal);
        activity?.SetTag(TagNames.Operation, ActivityNames.Output);
        activity?.SetTag(TagNames.OutputAdapter, adapterId);
        activity?.SetTag(TagNames.AlgorithmId, algorithmId);
        return activity;
    }

    internal static void RecordSuccess(Activity? activity, long startedAt, string operation, int? nodeCount = null, int? edgeCount = null, string? mode = null)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var tags = new TagList { { TagNames.Operation, operation } };
        if (mode is not null)
        {
            tags.Add(TagNames.Mode, mode);
        }

        OperationCounter.Add(1, tags);
        OperationDuration.Record(elapsed, tags);
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag(GormDiagnostics.TagNames.DurationMilliseconds, elapsed);
        activity?.SetTag(TagNames.Mode, mode);
        if (nodeCount is not null)
        {
            activity?.SetTag(TagNames.NodeCount, nodeCount.Value);
        }

        if (edgeCount is not null)
        {
            activity?.SetTag(TagNames.EdgeCount, edgeCount.Value);
        }
    }

    internal static void RecordFailure(Activity? activity, long startedAt, string operation, Exception exception)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var tags = new TagList { { TagNames.Operation, operation }, { GormDiagnostics.TagNames.ExceptionType, exception.GetType().FullName } };
        OperationCounter.Add(1, tags);
        OperationDuration.Record(elapsed, tags);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag(GormDiagnostics.TagNames.DurationMilliseconds, elapsed);
        activity?.SetTag(GormDiagnostics.TagNames.ExceptionType, exception.GetType().FullName);
        activity?.SetTag(GormDiagnostics.TagNames.ExceptionMessage, exception.Message);
    }
}