using Gorm.Application.History.Envelopes;
using Gorm.Application.History.Resolvers;

namespace Gorm.Application.History.Querying;

/// <summary>
/// Provides temporal query helpers for graph history envelopes.
/// </summary>
public static class GraphTemporalQueryExtensions
{
    /// <summary>
    /// Filters history entries to the snapshot valid at the specified UTC instant.
    /// </summary>
    /// <param name="history">The history query.</param>
    /// <param name="instantUtc">The UTC instant.</param>
    /// <returns>The filtered history query.</returns>
    public static IQueryable<GraphHistoryEnvelope> AsOf(this IQueryable<GraphHistoryEnvelope> history, DateTime instantUtc)
    {
        ArgumentNullException.ThrowIfNull(history);

        return new DefaultGraphTemporalResolver().ResolveAsOf(history, instantUtc).AsQueryable();
    }

    /// <summary>
    /// Filters history entries that overlap the specified UTC range.
    /// </summary>
    /// <param name="history">The history query.</param>
    /// <param name="fromUtc">The start UTC instant.</param>
    /// <param name="toUtc">The end UTC instant.</param>
    /// <returns>The filtered history query.</returns>
    public static IQueryable<GraphHistoryEnvelope> Between(this IQueryable<GraphHistoryEnvelope> history, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(history);

        var utcFrom = EnsureUtc(fromUtc);
        var utcTo = EnsureUtc(toUtc);

        if (utcTo < utcFrom)
        {
            throw new ArgumentException("The end instant must be greater than or equal to the start instant.", nameof(toUtc));
        }

        return history.Where(x => x.CapturedAtUtc >= utcFrom && x.CapturedAtUtc <= utcTo);
    }

    /// <summary>
    /// Filters history entries to records that are currently valid.
    /// </summary>
    /// <param name="history">The history query.</param>
    /// <returns>The filtered history query.</returns>
    public static IQueryable<GraphHistoryEnvelope> Current(this IQueryable<GraphHistoryEnvelope> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return new DefaultGraphTemporalResolver().ResolveCurrent(history).AsQueryable();
    }

    /// <summary>
    /// Orders history entries by capture timestamp ascending.
    /// </summary>
    /// <param name="history">The history query.</param>
    /// <returns>The ordered history query.</returns>
    public static IOrderedQueryable<GraphHistoryEnvelope> OrderByCapturedAt(this IQueryable<GraphHistoryEnvelope> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return history.OrderBy(x => x.CapturedAtUtc);
    }

    /// <summary>
    /// Orders history entries by validity window ascending.
    /// </summary>
    /// <param name="history">The history query.</param>
    /// <returns>The ordered history query.</returns>
    public static IOrderedQueryable<GraphHistoryEnvelope> OrderByValidity(this IQueryable<GraphHistoryEnvelope> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return history.OrderBy(x => x.ValidFromUtc).ThenBy(x => x.ValidToUtc);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}