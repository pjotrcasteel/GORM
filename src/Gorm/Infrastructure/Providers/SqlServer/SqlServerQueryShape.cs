using Gorm.Application.Querying.Models;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Precomputed SQL-relevant shape for a graph query.
/// </summary>
internal sealed class SqlServerQueryShape
{
    /// <summary>
    /// Gets or sets the non-projected filter steps.
    /// </summary>
    public required GraphFilterStep[] BaseFilters { get; init; }

    /// <summary>
    /// Gets or sets the projected filter steps.
    /// </summary>
    public required GraphFilterStep[] ProjectedFilters { get; init; }

    /// <summary>
    /// Gets or sets the non-projected orderings.
    /// </summary>
    public required GraphOrdering[] BaseOrderings { get; init; }

    /// <summary>
    /// Gets or sets the projected orderings.
    /// </summary>
    public required GraphOrdering[] ProjectedOrderings { get; init; }

    /// <summary>
    /// Gets a value indicating whether the query has whole entity projection.
    /// </summary>
    public required bool HasWholeEntityProjection { get; init; }

    /// <summary>
    /// Gets a value indicating whether projected operations exist.
    /// </summary>
    public bool HasProjectedOperations => ProjectedFilters.Length > 0 || ProjectedOrderings.Length > 0;

    /// <summary>
    /// Gets a value indicating whether projected filters exist.
    /// </summary>
    public bool HasProjectedFilters => ProjectedFilters.Length > 0;

    /// <summary>
    /// Gets a value indicating whether projected orderings exist.
    /// </summary>
    public bool HasProjectedOrderings => ProjectedOrderings.Length > 0;

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public static SqlServerQueryShape Create(GraphQueryModel queryModel)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var baseFilters = new List<GraphFilterStep>();
        var projectedFilters = new List<GraphFilterStep>();

        for (var i = 0; i < queryModel.Steps.Count; i++)
        {
            if (queryModel.Steps[i] is not GraphFilterStep filter)
            {
                continue;
            }

            if (filter.IsProjected)
            {
                projectedFilters.Add(filter);
                continue;
            }

            baseFilters.Add(filter);
        }

        var baseOrderings = new List<GraphOrdering>();
        var projectedOrderings = new List<GraphOrdering>();

        for (var i = 0; i < queryModel.Orderings.Count; i++)
        {
            var ordering = queryModel.Orderings[i];

            if (ordering.IsProjected)
            {
                projectedOrderings.Add(ordering);
                continue;
            }

            baseOrderings.Add(ordering);
        }

        return new SqlServerQueryShape
        {
            BaseFilters = [.. baseFilters],
            ProjectedFilters = [.. projectedFilters],
            BaseOrderings = [.. baseOrderings],
            ProjectedOrderings = [.. projectedOrderings],
            HasWholeEntityProjection = HasWholeEntityProjection2(queryModel.Projection)
        };
    }

    private static bool HasWholeEntityProjection2(GraphQueryProjection? projection)
    {
        switch (projection)
        {
            case GraphObjectProjection objectProjection:
                for (var i = 0; i < objectProjection.Bindings.Count; i++)
                {
                    if (objectProjection.Bindings[i].IsWholeEntity)
                    {
                        return true;
                    }
                }

                return false;

            case GraphConstructorProjection constructorProjection:
                for (var i = 0; i < constructorProjection.Parameters.Count; i++)
                {
                    if (constructorProjection.Parameters[i].IsWholeEntity)
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }
}