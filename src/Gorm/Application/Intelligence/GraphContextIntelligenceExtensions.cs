using Gorm.Application.Context;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence;

/// <summary>
/// Adds graph intelligence projection operations to a graph context.
/// </summary>
public static class GraphContextIntelligenceExtensions
{
    /// <summary>
    /// Materializes ordinary GORM node and edge queries into a detached intelligence projection.
    /// </summary>
    public static Task<GraphProjection> ProjectGraphAsync(this GraphContext context, Action<GraphProjectionQueryBuilder> configure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new GraphProjectionQueryBuilder(context);
        configure(builder);
        return builder.BuildAsync(cancellationToken);
    }

    /// <summary>
    /// Materializes ordinary GORM queries into a detached projection with an explicit key and source version.
    /// </summary>
    public static Task<GraphProjectionSnapshot> ProjectGraphSnapshotAsync(
        this GraphContext context,
        GraphProjectionKey key,
        long version,
        Action<GraphProjectionQueryBuilder> configure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(configure);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "A projection version cannot be negative.");
        }
        return ProjectGraphSnapshotCoreAsync(context, key, version, configure, cancellationToken);
    }

    private static async Task<GraphProjectionSnapshot> ProjectGraphSnapshotCoreAsync(
        GraphContext context,
        GraphProjectionKey key,
        long version,
        Action<GraphProjectionQueryBuilder> configure,
        CancellationToken cancellationToken)
    {
        var projection = await context.ProjectGraphAsync(configure, cancellationToken).ConfigureAwait(false);
        return new GraphProjectionSnapshot(key, version, projection);
    }

    /// <summary>
    /// Gets or materializes a version-checked projection snapshot through an explicit projection cache.
    /// </summary>
    public static Task<GraphProjectionCacheResult> ProjectGraphCachedAsync(
        this GraphContext context,
        GraphProjectionCache cache,
        GraphProjectionKey key,
        long sourceVersion,
        Action<GraphProjectionQueryBuilder> configure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(configure);

        return cache.GetOrCreateAsync(key, sourceVersion, token => context.ProjectGraphSnapshotAsync(key, sourceVersion, configure, token), cancellationToken);
    }
}