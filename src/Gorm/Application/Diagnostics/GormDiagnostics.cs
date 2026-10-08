using System.Diagnostics;
using System.Diagnostics.Metrics;
using Gorm.Application.Context;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Diagnostics;

/// <summary>
/// Provides built-in diagnostics names and helpers for GORM instrumentation.
/// </summary>
public static class GormDiagnostics
{
    /// <summary>
    /// Gets the activity source name.
    /// </summary>
    public const string ActivitySourceName = "Gorm";

    /// <summary>
    /// Gets the meter name.
    /// </summary>
    public const string MeterName = "Gorm";

    /// <summary>
    /// Gets the shared activity source.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

    /// <summary>
    /// Gets the shared meter.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName);

    private static readonly Counter<long> QueryCounter = Meter.CreateCounter<long>(MetricNames.QueryCount);
    private static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>(MetricNames.QueryDurationMilliseconds);

    /// <summary>
    /// Activity names emitted by GORM.
    /// </summary>
    public static class ActivityNames
    {
        /// <summary>
        /// Gets the list query activity name.
        /// </summary>
        public const string QueryList = "gorm.query.list";

        /// <summary>
        /// Gets the scalar query activity name.
        /// </summary>
        public const string QueryScalar = "gorm.query.scalar";
    }

    /// <summary>
    /// Metric names emitted by GORM.
    /// </summary>
    public static class MetricNames
    {
        /// <summary>
        /// Gets the query counter metric name.
        /// </summary>
        public const string QueryCount = "gorm.query.count";

        /// <summary>
        /// Gets the query duration histogram metric name.
        /// </summary>
        public const string QueryDurationMilliseconds = "gorm.query.duration.ms";
    }

    /// <summary>
    /// Activity tag names emitted by GORM.
    /// </summary>
    public static class TagNames
    {
        /// <summary>
        /// Gets the database system tag name.
        /// </summary>
        public const string DatabaseSystem = "db.system";

        /// <summary>
        /// Gets the graph context tag name.
        /// </summary>
        public const string Context = "gorm.context";

        /// <summary>
        /// Gets the result type tag name.
        /// </summary>
        public const string ResultType = "gorm.result.type";

        /// <summary>
        /// Gets the SQL length tag name.
        /// </summary>
        public const string SqlLength = "gorm.sql.length";

        /// <summary>
        /// Gets the parameter count tag name.
        /// </summary>
        public const string ParameterCount = "gorm.parameter.count";

        /// <summary>
        /// Gets the duration tag name.
        /// </summary>
        public const string DurationMilliseconds = "gorm.duration.ms";

        /// <summary>
        /// Gets the row count tag name.
        /// </summary>
        public const string RowCount = "gorm.row.count";

        /// <summary>
        /// Gets the exception type tag name.
        /// </summary>
        public const string ExceptionType = "exception.type";

        /// <summary>
        /// Gets the exception message tag name.
        /// </summary>
        public const string ExceptionMessage = "exception.message";
    }

    /// <summary>
    /// Reusable activity tag values emitted by GORM.
    /// </summary>
    public static class TagValues
    {
        /// <summary>
        /// Gets the SQL Server database system tag value.
        /// </summary>
        public const string SqlServer = "mssql";
    }

    /// <summary>
    /// Starts a query activity when tracing is enabled.
    /// </summary>
    /// <param name="operationName">The operation name.</param>
    /// <param name="context">The graph context.</param>
    /// <param name="sql">The generated SQL.</param>
    /// <param name="resultType">The result type.</param>
    /// <returns>The activity, if tracing is enabled.</returns>
    public static Activity? StartQueryActivity(string operationName, GraphContext context, GraphSqlQuery sql, Type resultType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(resultType);

        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Client);
        activity?.SetTag(TagNames.DatabaseSystem, TagValues.SqlServer);
        activity?.SetTag(TagNames.Context, context.GetType().FullName);
        activity?.SetTag(TagNames.ResultType, resultType.FullName);
        activity?.SetTag(TagNames.SqlLength, sql.CommandText.Length);
        activity?.SetTag(TagNames.ParameterCount, sql.Parameters.Count);
        return activity;
    }

    /// <summary>
    /// Records a successful query.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="startedAt">The starting timestamp.</param>
    /// <param name="rowCount">The row count.</param>
    public static void RecordQuerySuccess(Activity? activity, long startedAt, int? rowCount = null)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        QueryCounter.Add(1);
        QueryDuration.Record(elapsedMs);
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag(TagNames.DurationMilliseconds, elapsedMs);

        if (rowCount is not null)
        {
            activity?.SetTag(TagNames.RowCount, rowCount.Value);
        }
    }

    /// <summary>
    /// Records a failed query.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="startedAt">The starting timestamp.</param>
    /// <param name="exception">The exception.</param>
    public static void RecordQueryFailure(Activity? activity, long startedAt, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        QueryCounter.Add(1);
        QueryDuration.Record(elapsedMs);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag(TagNames.DurationMilliseconds, elapsedMs);
        activity?.SetTag(TagNames.ExceptionType, exception.GetType().FullName);
        activity?.SetTag(TagNames.ExceptionMessage, exception.Message);
    }
}