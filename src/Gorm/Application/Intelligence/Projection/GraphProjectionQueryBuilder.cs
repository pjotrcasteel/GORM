using System.Diagnostics;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Querying;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Configures a graph projection using ordinary GORM queries.
/// </summary>
public sealed class GraphProjectionQueryBuilder
{
    private readonly GraphContext _context;
    private readonly List<Func<GraphProjectionBuilder, CancellationToken, Task>> _sources = [];

    internal GraphProjectionQueryBuilder(GraphContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets or sets how edges whose endpoints are outside the selected nodes are handled.
    /// </summary>
    public GraphOrphanedEdgeBehavior OrphanedEdgeBehavior { get; set; } = GraphOrphanedEdgeBehavior.Throw;

    /// <summary>
    /// Adds a GORM node query to the projection.
    /// </summary>
    public GraphProjectionQueryBuilder Nodes<TNode>(IQueryable<TNode> query)
        where TNode : Node
    {
        EnsureQueryBelongsToContext(query);
        _sources.Add(async (builder, cancellationToken) =>
        {
            var nodes = await query.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            builder.AddNodes(nodes);
        });

        return this;
    }

    /// <summary>
    /// Adds a GORM edge query to the projection.
    /// </summary>
    public GraphProjectionQueryBuilder Edges<TEdge>(IQueryable<TEdge> query)
        where TEdge : Edge
    {
        EnsureQueryBelongsToContext(query);
        _sources.Add(async (builder, cancellationToken) =>
        {
            var edges = await query.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            builder.AddEdges(edges);
        });

        return this;
    }

    internal async Task<GraphProjection> BuildAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var activity = GormIntelligenceDiagnostics.StartProjectionActivity(_sources.Count);
        var builder = new GraphProjectionBuilder(new GraphProjectionOptions
        {
            OrphanedEdgeBehavior = OrphanedEdgeBehavior
        });

        try
        {
            foreach (var source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await source(builder, cancellationToken).ConfigureAwait(false);
            }

            var projection = builder.Build();
            GormIntelligenceDiagnostics.RecordSuccess(
                activity,
                startedAt,
                GormIntelligenceDiagnostics.ActivityNames.Projection,
                projection.Statistics.NodeCount,
                projection.Statistics.EdgeCount);
            return projection;
        }
        catch (Exception exception)
        {
            GormIntelligenceDiagnostics.RecordFailure(activity, startedAt, GormIntelligenceDiagnostics.ActivityNames.Projection, exception);
            throw;
        }
    }

    private void EnsureQueryBelongsToContext<T>(IQueryable<T> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Provider is not GraphQueryProvider provider || !ReferenceEquals(provider.Context, _context))
        {
            throw new ArgumentException("Every projection query must be a GORM query created by the projected context.", nameof(query));
        }
    }
}