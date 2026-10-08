using System.Text;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;
using Gorm.Infrastructure.Providers.SqlServer.Resolvers;
using Gorm.Infrastructure.Providers.SqlServer.Writers;
using Gorm.Infrastructure.Sql;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Represents sql server graph query sql generator.
/// </summary>
public static class SqlServerGraphQuerySqlGenerator
{
    private const string QueryAlias = "q";
    private const string Select = "SELECT";

    /// <summary>
    /// Executes generate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel) => Generate(model, queryModel, effectiveTakeOverride: null);

    /// <summary>
    /// Executes generate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="effectiveTakeOverride">The effective take override.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(queryModel);

        var shape = SqlServerQueryShape.Create(queryModel);
        ValidateQueryModel(queryModel);

        return shape.HasProjectedOperations ? GenerateProjected(model, queryModel, shape, effectiveTakeOverride) : GenerateBase(model, queryModel, shape, effectiveTakeOverride);
    }

    /// <summary>
    /// Executes generate exists.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="effectiveTakeOverride">The effective take override.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery GenerateExists(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(queryModel);

        var shape = SqlServerQueryShape.Create(queryModel);
        ValidateQueryModel(queryModel);

        if (shape.HasProjectedOperations)
        {
            var projected = GenerateProjectedForWrapper(model, queryModel, shape, ShouldPreserveOrderingInScalarWrapper(queryModel));
            var builder = new StringBuilder(projected.CommandText.Length + 64);

            AppendSelectHeadForExists(builder, queryModel, effectiveTakeOverride);
            builder.AppendLine(" 1");
            AppendWrappedQuery(builder, projected.CommandText);

            return BuildGraphSqlQuery(builder, projected.Parameters);
        }

        var context = BuildContext(model, queryModel);
        var baseBuilder = new StringBuilder();

        AppendSelectHeadForExists(baseBuilder, queryModel, effectiveTakeOverride);
        baseBuilder.AppendLine(" 1");

        AppendFrom(baseBuilder, context);
        context.AppendWhere(baseBuilder);
        AppendOrderBy(baseBuilder, shape, queryModel, context);
        AppendPaging(baseBuilder, queryModel, effectiveTakeOverride);

        return BuildGraphSqlQuery(context, baseBuilder);
    }

    /// <summary>
    /// Executes generate aggregate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="aggregateKind">The aggregate kind.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery GenerateAggregate(GraphModel model, GraphQueryModel queryModel, GraphAggregateKind aggregateKind)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(queryModel);

        var shape = SqlServerQueryShape.Create(queryModel);
        ValidateQueryModel(queryModel);
        EnsureScalarAggregateProjection(queryModel, aggregateKind);

        var preserveOrdering = ShouldPreserveOrderingInScalarWrapper(queryModel);
        var innerQuery = shape.HasProjectedOperations
            ? GenerateProjectedForWrapper(model, queryModel, shape, preserveOrdering)
            : GenerateScalarWrapperInnerQuery(model, queryModel, shape, preserveOrdering);

        var builder = new StringBuilder(innerQuery.CommandText.Length + 64);
        builder.Append("SELECT ");
        builder.Append(GetAggregateSqlName(aggregateKind));
        builder.AppendLine("([Value])");
        AppendWrappedQuery(builder, innerQuery.CommandText);

        return BuildGraphSqlQuery(builder, innerQuery.Parameters);
    }

    /// <summary>
    /// Executes generate count.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel) =>
        GenerateCountCore(model, queryModel, countExpression: "COUNT(*)");

    /// <summary>
    /// Executes generate long count.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public static GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel) =>
        GenerateCountCore(model, queryModel, countExpression: "COUNT_BIG(*)");

    private static GraphSqlQuery GenerateBase(GraphModel model, GraphQueryModel queryModel, SqlServerQueryShape shape, int? effectiveTakeOverride)
    {
        var context = BuildContext(model, queryModel);
        var builder = new StringBuilder();

        AppendSelectHead(builder, queryModel, effectiveTakeOverride);
        builder.Append(' ');
        AppendSelectList(builder, queryModel, context);
        builder.AppendLine();

        AppendFrom(builder, context);
        context.AppendWhere(builder);
        AppendOrderBy(builder, shape, queryModel, context);
        AppendPaging(builder, queryModel, effectiveTakeOverride);

        return BuildGraphSqlQuery(context, builder);
    }

    private static GraphSqlQuery GenerateCountCore(GraphModel model, GraphQueryModel queryModel, string countExpression)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(queryModel);

        var shape = SqlServerQueryShape.Create(queryModel);
        ValidateQueryModel(queryModel);

        var preserveOrdering = ShouldPreserveOrderingInScalarWrapper(queryModel);
        var innerQuery = shape.HasProjectedOperations
            ? GenerateProjectedForWrapper(model, queryModel, shape, preserveOrdering)
            : GenerateCountInnerQuery(model, queryModel, shape, preserveOrdering);

        var builder = new StringBuilder(innerQuery.CommandText.Length + 64);
        builder.Append("SELECT ");
        builder.AppendLine(countExpression);
        AppendWrappedQuery(builder, innerQuery.CommandText);

        return BuildGraphSqlQuery(builder, innerQuery.Parameters);
    }

    private static GraphSqlQuery GenerateProjectedForWrapper(GraphModel model, GraphQueryModel queryModel, SqlServerQueryShape shape, bool preserveOrdering)
    {
        var context = BuildContext(model, queryModel);
        var innerBuilder = BuildInnerProjectedQuery(queryModel, shape, context, preserveBaseOrdering: preserveOrdering && queryModel.SkipCount is not null);

        var outerBuilder = new StringBuilder(innerBuilder.Length + 128);
        AppendOuterSelectHeadForWrapper(outerBuilder, queryModel, preserveOrdering);
        outerBuilder.Append(' ');
        AppendOuterSelectList(outerBuilder, queryModel);
        outerBuilder.AppendLine();
        AppendWrappedQuery(outerBuilder, innerBuilder.ToString());

        var outerParameters = new List<GraphSqlParameter>(shape.ProjectedFilters.Length);
        AppendProjectedWhere(outerBuilder, shape, queryModel, outerParameters);

        if (preserveOrdering)
        {
            AppendProjectedOrderBy(outerBuilder, shape);
            AppendPagingOnOuterQuery(outerBuilder, shape, queryModel, effectiveTakeOverride: null);
        }

        var query = BuildGraphSqlQuery(context, outerBuilder);
        AddParameters(query, outerParameters);

        return query;
    }

    private static GraphSqlQuery GenerateProjected(GraphModel model, GraphQueryModel queryModel, SqlServerQueryShape shape, int? effectiveTakeOverride)
    {
        var context = BuildContext(model, queryModel);
        var wrapsProjectedQuery = shape.HasProjectedFilters || shape.HasProjectedOrderings;
        var innerBuilder = BuildInnerProjectedQuery(queryModel, shape, context, preserveBaseOrdering: !wrapsProjectedQuery || queryModel.SkipCount is not null);

        if (!shape.HasProjectedFilters &&
            !shape.HasProjectedOrderings &&
            !shape.HasWholeEntityProjection)
        {
            return BuildGraphSqlQuery(context, innerBuilder);
        }

        if (shape.HasWholeEntityProjection)
        {
            if (shape.HasProjectedFilters || shape.HasProjectedOrderings || queryModel.SkipCount is not null || effectiveTakeOverride is not null)
            {
                throw new NotSupportedException("Whole-entity Select(...) projections do not support post-projection filtering, ordering, or paging yet.");
            }

            return BuildGraphSqlQuery(context, innerBuilder);
        }

        var outerBuilder = new StringBuilder(innerBuilder.Length + 128);
        AppendOuterSelectHead(outerBuilder, queryModel, effectiveTakeOverride);
        outerBuilder.Append(' ');
        AppendOuterSelectList(outerBuilder, queryModel);
        outerBuilder.AppendLine();
        AppendWrappedQuery(outerBuilder, innerBuilder.ToString());

        var outerParameters = new List<GraphSqlParameter>(shape.ProjectedFilters.Length);
        AppendProjectedWhere(outerBuilder, shape, queryModel, outerParameters);
        AppendProjectedOrderBy(outerBuilder, shape);
        AppendPagingOnOuterQuery(outerBuilder, shape, queryModel, effectiveTakeOverride);

        var query = BuildGraphSqlQuery(context, outerBuilder);
        AddParameters(query, outerParameters);

        return query;
    }

    private static StringBuilder BuildInnerProjectedQuery(GraphQueryModel queryModel, SqlServerQueryShape shape, SqlGenerationContext context, bool preserveBaseOrdering)
    {
        var builder = new StringBuilder();

        AppendSelectHeadForInnerProjectedQuery(builder, queryModel);
        builder.Append(' ');
        AppendSelectList(builder, queryModel, context);
        builder.AppendLine();

        AppendFrom(builder, context);
        context.AppendWhere(builder);

        if (preserveBaseOrdering)
        {
            AppendBaseOrderBy(builder, shape, queryModel, context);
        }

        return builder;
    }

    private static GraphSqlQuery GenerateScalarWrapperInnerQuery(GraphModel model, GraphQueryModel queryModel, SqlServerQueryShape shape, bool preserveOrdering)
    {
        var context = BuildContext(model, queryModel);
        var builder = new StringBuilder();

        AppendSelectHead(builder, queryModel, effectiveTakeOverride: null);
        builder.Append(' ');
        AppendSelectList(builder, queryModel, context);
        builder.AppendLine();

        AppendFrom(builder, context);
        context.AppendWhere(builder);

        if (preserveOrdering)
        {
            AppendOrderBy(builder, shape, queryModel, context);
            AppendPaging(builder, queryModel, effectiveTakeOverride: null);
        }

        return BuildGraphSqlQuery(context, builder);
    }

    private static GraphSqlQuery GenerateCountInnerQuery(GraphModel model, GraphQueryModel queryModel, SqlServerQueryShape shape, bool preserveOrdering)
    {
        var context = BuildContext(model, queryModel);
        var builder = new StringBuilder();

        AppendSelectHead(builder, queryModel, effectiveTakeOverride: null);
        builder.Append(' ');
        AppendCountInnerSelectPrefix(builder, queryModel);
        AppendSelectList(builder, queryModel, context);
        builder.AppendLine();

        AppendFrom(builder, context);
        context.AppendWhere(builder);

        if (preserveOrdering)
        {
            AppendOrderBy(builder, shape, queryModel, context);
            AppendPaging(builder, queryModel, effectiveTakeOverride: null);
        }

        return BuildGraphSqlQuery(context, builder);
    }

    private static void EnsureScalarAggregateProjection(GraphQueryModel queryModel, GraphAggregateKind aggregateKind)
    {
        if (queryModel.Projection is GraphScalarProjection)
        {
            return;
        }

        throw new NotSupportedException($"{aggregateKind} requires a scalar projection. Use Select(...) first or use the selector overload.");
    }

    private static string GetAggregateSqlName(GraphAggregateKind aggregateKind) =>
        aggregateKind switch
        {
            GraphAggregateKind.Sum => "SUM",
            GraphAggregateKind.Average => "AVG",
            GraphAggregateKind.Min => "MIN",
            GraphAggregateKind.Max => "MAX",
            _ => throw new NotSupportedException($"Aggregate '{aggregateKind}' is not supported.")
        };

    private static bool ShouldPreserveOrderingInScalarWrapper(GraphQueryModel queryModel) =>
        queryModel.SkipCount is not null || queryModel.TakeCount is not null;

    private static void ValidateQueryModel(GraphQueryModel queryModel)
    {
        if (queryModel.IsDistinct && queryModel.Orderings.Count > 0)
        {
            throw new NotSupportedException("Distinct() with OrderBy(...) is not supported yet.");
        }
    }

    private static SqlGenerationContext BuildContext(GraphModel model, GraphQueryModel queryModel)
    {
        var context = new SqlGenerationContext(model, queryModel.RootElementType, queryModel.RootElementKind);

        for (var i = 0; i < queryModel.Steps.Count; i++)
        {
            switch (queryModel.Steps[i])
            {
                case GraphFilterStep filter when !filter.IsProjected:
                    AddFilter(context, filter);
                    break;

                case GraphFilterStep:
                    break;

                case GraphTraversalStep traversal:
                    AddTraversal(context, traversal);
                    break;

                case GraphEdgeNodeTraversalStep edgeTraversal:
                    AddEdgeEndpointTraversal(context, edgeTraversal);
                    break;

                default:
                    throw new NotSupportedException($"Query step '{queryModel.Steps[i].GetType().FullName}' is not supported.");
            }
        }

        return context;
    }

    private static void AddTraversal(SqlGenerationContext context, GraphTraversalStep traversal)
    {
        var edgeMapping = context.GetEdge(traversal.EdgeType);
        var outputNodeMapping = context.GetNode(traversal.OutputElementType);

        var sourceNodeAlias = context.CurrentAlias;
        var edgeAlias = context.NextAlias("e");
        var targetNodeAlias = context.NextAlias("n");
        var isOutgoing = traversal.Direction == GraphTraversalDirection.Outgoing;

        context.AddFromItem(edgeMapping, edgeAlias);
        context.AddFromItem(outputNodeMapping, targetNodeAlias);

        var predicateMatch = isOutgoing
            ? $"MATCH({sourceNodeAlias}-({edgeAlias})->{targetNodeAlias})"
            : $"MATCH({targetNodeAlias}-({edgeAlias})->{sourceNodeAlias})";

        context.MatchPredicates.Add(predicateMatch);

        if (traversal.EdgePredicate is not null)
        {
            var predicateWriter = new PredicateWriter(context, traversal.EdgePredicate.Parameters[0], edgeAlias);
            context.WherePredicates.Add(predicateWriter.Write(traversal.EdgePredicate.Body));
        }

        context.LastTraversalEdgeAlias = edgeAlias;
        context.LastTraversalEdgeType = traversal.EdgeType;
        context.LastTraversalFromNodeAlias = isOutgoing ? sourceNodeAlias : targetNodeAlias;
        context.LastTraversalToNodeAlias = isOutgoing ? targetNodeAlias : sourceNodeAlias;
        context.CurrentAlias = targetNodeAlias;
        context.CurrentElementType = traversal.OutputElementType;
        context.CurrentElementKind = GraphQueryElementKind.Node;
    }

    private static void AddEdgeEndpointTraversal(SqlGenerationContext context, GraphEdgeNodeTraversalStep traversal)
    {
        if (context.CurrentElementKind != GraphQueryElementKind.Edge)
        {
            throw new NotSupportedException($"Edge endpoint {nameof(traversal)} requires the current query element to be an edge.");
        }

        var currentEdgeMapping = context.GetEdge(context.CurrentElementType);
        var outputNodeMapping = context.GetNode(traversal.OutputElementType);

        var expectedNodeType = traversal.Endpoint == GraphEdgeEndpoint.From ? currentEdgeMapping.FromNodeType : currentEdgeMapping.ToNodeType;

        if (expectedNodeType != traversal.OutputElementType)
        {
            throw new InvalidOperationException(
                $"Edge '{currentEdgeMapping.ClrType.Name}' does not connect its {traversal.Endpoint} side to node '{traversal.OutputElementType.Name}'.");
        }

        var nodeAlias = context.NextAlias("n");

        context.AddFromItem(outputNodeMapping, nodeAlias);

        var edgePseudoColumn = traversal.Endpoint == GraphEdgeEndpoint.From ? "$from_id" : "$to_id";

        context.WherePredicates.Add($"({SqlGenerationHelpers.Escape(nodeAlias)}.$node_id = {SqlGenerationHelpers.Escape(context.CurrentAlias)}.{edgePseudoColumn})");

        context.CurrentAlias = nodeAlias;
        context.CurrentElementType = traversal.OutputElementType;
        context.CurrentElementKind = GraphQueryElementKind.Node;
    }

    private static void AddFilter(SqlGenerationContext context, GraphFilterStep filter)
    {
        var lambda = filter.Predicate;
        var predicateWriter = new PredicateWriter(context, lambda.Parameters[0], context.CurrentAlias);

        context.WherePredicates.Add(predicateWriter.Write(lambda.Body));
    }

    private static void AppendFrom(StringBuilder builder, SqlGenerationContext context)
    {
        builder.Append("FROM ");
        context.AppendFromItems(builder);
    }

    private static void AppendOrderBy(StringBuilder builder, SqlServerQueryShape shape, GraphQueryModel queryModel, SqlGenerationContext context)
    {
        AppendBaseOrderBy(builder, shape, queryModel, context);
    }

    private static void AppendOrderBy(StringBuilder builder, string alias, GraphOrdering[] orderings)
    {
        builder.Append("ORDER BY ");

        for (var i = 0; i < orderings.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Column(alias, orderings[i].PropertyName));
            builder.Append(orderings[i].Descending ? " DESC" : " ASC");
        }

        builder.AppendLine();
    }

    private static void AppendProjectedOrderBy(StringBuilder builder, SqlServerQueryShape shape)
    {
        if (shape.ProjectedOrderings.Length > 0)
        {
            AppendOrderBy(builder, QueryAlias, shape.ProjectedOrderings);
        }
    }

    private static void AppendBaseOrderBy(StringBuilder builder, SqlServerQueryShape shape, GraphQueryModel queryModel, SqlGenerationContext context)
    {
        if (shape.BaseOrderings.Length > 0)
        {
            AppendOrderBy(builder, context.CurrentAlias, shape.BaseOrderings);
            return;
        }

        if (queryModel.SkipCount is not null)
        {
            builder.Append("ORDER BY ");
            builder.Append(Column(context.CurrentAlias, context.GetCurrentKeyPropertyName));
            builder.AppendLine(" ASC");
        }
    }

    private static void AppendProjectedWhere(StringBuilder builder, SqlServerQueryShape shape, GraphQueryModel queryModel, IList<GraphSqlParameter> parameters)
    {
        if (shape.ProjectedFilters.Length == 0)
        {
            return;
        }

        var resolver = ProjectedColumnResolver.CreateProjectedColumnResolver(queryModel);
        var parameterCounter = 0;

        builder.Append("WHERE ");

        for (var i = 0; i < shape.ProjectedFilters.Length; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
                builder.Append("  AND ");
            }

            var predicate = shape.ProjectedFilters[i].Predicate;
            var writer = new ProjectedPredicateWriter(QueryAlias, predicate.Parameters[0], resolver, parameters, () => string.Concat("@p_outer_", parameterCounter++));

            builder.Append(writer.Write(predicate.Body));
        }

        builder.AppendLine();
    }

    private static void AppendPaging(StringBuilder builder, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        if (queryModel.SkipCount is null)
        {
            return;
        }

        var take = effectiveTakeOverride ?? queryModel.TakeCount;

        builder.Append("OFFSET ");
        builder.Append(queryModel.SkipCount.Value);
        builder.AppendLine(" ROWS");

        if (take is int takeCount)
        {
            builder.Append("FETCH NEXT ");
            builder.Append(takeCount);
            builder.AppendLine(" ROWS ONLY");
        }
    }

    private static void AppendPagingOnOuterQuery(StringBuilder builder, SqlServerQueryShape shape, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        if (queryModel.SkipCount is null)
        {
            return;
        }

        if (!shape.HasProjectedOrderings)
        {
            builder.Append("ORDER BY ");
            builder.Append(Column(QueryAlias, GetDefaultOuterOrderByColumn(queryModel)));
            builder.AppendLine(" ASC");
        }

        var take = effectiveTakeOverride ?? queryModel.TakeCount;

        builder.Append("OFFSET ");
        builder.Append(queryModel.SkipCount.Value);
        builder.AppendLine(" ROWS");

        if (take is int takeCount)
        {
            builder.Append("FETCH NEXT ");
            builder.Append(takeCount);
            builder.AppendLine(" ROWS ONLY");
        }
    }

    private static GraphSqlQuery BuildGraphSqlQuery(SqlGenerationContext context, StringBuilder builder)
    {
        var query = new GraphSqlQuery
        {
            CommandText = builder.ToString()
        };

        AddParameters(query, context.Parameters);

        return query;
    }

    private static GraphSqlQuery BuildGraphSqlQuery(StringBuilder builder, IEnumerable<GraphSqlParameter> parameters)
    {
        var query = new GraphSqlQuery
        {
            CommandText = builder.ToString()
        };

        AddParameters(query, parameters);

        return query;
    }

    private static void AddParameters(GraphSqlQuery query, IEnumerable<GraphSqlParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            query.Parameters.Add(parameter);
        }
    }

    private static void AppendSelectList(StringBuilder builder, GraphQueryModel queryModel, SqlGenerationContext context)
    {
        switch (queryModel.Projection)
        {
            case null:
                builder.Append(context.CurrentAlias);
                builder.Append(".*");
                break;

            case GraphScalarProjection scalarProjection:
                builder.Append(Column(context.GetProjectionAlias(scalarProjection.SourceKind), scalarProjection.SourcePropertyName));
                builder.Append(" AS ");
                builder.Append(SqlGenerationHelpers.Escape("Value"));
                break;

            case GraphObjectProjection objectProjection:
                AppendObjectProjectionSelectList(builder, context, objectProjection);
                break;

            case GraphConstructorProjection constructorProjection:
                AppendConstructorProjectionSelectList(builder, context, constructorProjection);
                break;

            default:
                throw new NotSupportedException($"Projection '{queryModel.Projection.GetType().FullName}' is not supported.");
        }
    }

    private static void AppendOuterSelectList(StringBuilder builder, GraphQueryModel queryModel)
    {
        switch (queryModel.Projection)
        {
            case GraphScalarProjection:
                builder.Append("[q].[Value] AS [Value]");
                break;

            case GraphObjectProjection objectProjection:
                AppendOuterObjectProjectionSelectList(builder, objectProjection);
                break;

            case GraphConstructorProjection constructorProjection:
                AppendOuterConstructorProjectionSelectList(builder, constructorProjection);
                break;

            case null:
                builder.Append("[q].*");
                break;

            default:
                throw new NotSupportedException($"Projection '{queryModel.Projection.GetType().FullName}' is not supported.");
        }
    }

    private static void AppendObjectProjectionSelectList(StringBuilder builder, SqlGenerationContext context, GraphObjectProjection projection)
    {
        var hasColumns = false;

        for (var i = 0; i < projection.Bindings.Count; i++)
        {
            var binding = projection.Bindings[i];

            if (binding.IsWholeEntity)
            {
                AppendWholeEntityProjectionColumns(builder, context, binding.TargetMemberName, binding.SourceKind, ref hasColumns);
                continue;
            }

            var sourcePropertyName = GetRequiredSourcePropertyName(binding);
            AppendProjectionColumn(builder, context.GetProjectionAlias(binding.SourceKind), sourcePropertyName, binding.TargetMemberName, ref hasColumns);
        }
    }

    private static void AppendConstructorProjectionSelectList(StringBuilder builder, SqlGenerationContext context, GraphConstructorProjection projection)
    {
        var hasColumns = false;

        for (var i = 0; i < projection.Parameters.Count; i++)
        {
            var parameter = projection.Parameters[i];

            if (parameter.IsWholeEntity)
            {
                AppendWholeEntityProjectionColumns(builder, context, parameter.ParameterName, parameter.SourceKind, ref hasColumns);
                continue;
            }

            var sourcePropertyName = GetRequiredSourcePropertyName(parameter);
            AppendProjectionColumn(builder, context.GetProjectionAlias(parameter.SourceKind), sourcePropertyName, parameter.ParameterName, ref hasColumns);
        }
    }

    private static void AppendWholeEntityProjectionColumns(
        StringBuilder builder,
        SqlGenerationContext context,
        string targetPrefix,
        GraphProjectionSourceKind sourceKind,
        ref bool hasColumns)
    {
        var sourceAlias = context.GetProjectionAlias(sourceKind);
        var propertyNames = context.GetProjectionPropertyNames(sourceKind);

        for (var index = 0; index < propertyNames.Length; index++)
        {
            AppendProjectionColumn(builder, sourceAlias, propertyNames[index], targetPrefix + "__" + propertyNames[index], ref hasColumns);
        }

        if (sourceKind != GraphProjectionSourceKind.Edge)
        {
            return;
        }

        var fromNodeAlias = context.LastTraversalFromNodeAlias
            ?? throw new InvalidOperationException("An edge projection requires a source node alias.");

        var toNodeAlias = context.LastTraversalToNodeAlias
            ?? throw new InvalidOperationException("An edge projection requires a destination node alias.");

        AppendProjectionColumn(builder, fromNodeAlias, nameof(Node.Id), targetPrefix + "__" + nameof(Edge.FromId), ref hasColumns);
        AppendProjectionColumn(builder, toNodeAlias, nameof(Node.Id), targetPrefix + "__" + nameof(Edge.ToId), ref hasColumns);
    }

    private static void AppendProjectionColumn(StringBuilder builder, string sourceAlias, string sourcePropertyName, string targetColumnAlias, ref bool hasColumns)
    {
        if (hasColumns)
        {
            builder.Append(", ");
        }

        builder.Append(Column(sourceAlias, sourcePropertyName));
        builder.Append(" AS ");
        builder.Append(SqlGenerationHelpers.Escape(targetColumnAlias));

        hasColumns = true;
    }

    private static void AppendOuterObjectProjectionSelectList(StringBuilder builder, GraphObjectProjection projection)
    {
        var hasColumns = false;

        for (var i = 0; i < projection.Bindings.Count; i++)
        {
            var binding = projection.Bindings[i];

            if (binding.IsWholeEntity)
            {
                continue;
            }

            AppendOuterColumn(builder, binding.TargetMemberName, ref hasColumns);
        }

        if (!hasColumns)
        {
            builder.Append("[q].*");
        }
    }

    private static void AppendOuterConstructorProjectionSelectList(StringBuilder builder, GraphConstructorProjection projection)
    {
        var hasColumns = false;

        for (var i = 0; i < projection.Parameters.Count; i++)
        {
            var parameter = projection.Parameters[i];

            if (parameter.IsWholeEntity)
            {
                continue;
            }

            AppendOuterColumn(builder, parameter.ParameterName, ref hasColumns);
        }

        if (!hasColumns)
        {
            builder.Append("[q].*");
        }
    }

    private static void AppendOuterColumn(StringBuilder builder, string columnName, ref bool hasColumns)
    {
        if (hasColumns)
        {
            builder.Append(", ");
        }

        builder.Append(Column(QueryAlias, columnName));
        builder.Append(" AS ");
        builder.Append(SqlGenerationHelpers.Escape(columnName));

        hasColumns = true;
    }

    private static string GetRequiredSourcePropertyName(GraphProjectionBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.SourcePropertyName))
        {
            return binding.SourcePropertyName;
        }

        throw new NotSupportedException($"Projection {nameof(binding)} '{binding.TargetMemberName}' does not have a source property name.");
    }

    private static string GetRequiredSourcePropertyName(GraphConstructorProjectionParameter parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter.SourcePropertyName))
        {
            return parameter.SourcePropertyName;
        }

        throw new NotSupportedException($"Projection {nameof(parameter)} '{parameter.ParameterName}' does not have a source property name.");
    }

    private static string Column(string tableAlias, string columnName) => SqlGenerationHelpers.EscapeColumn(tableAlias, columnName);

    private static void AppendSelectHead(StringBuilder builder, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        var effectiveTake = effectiveTakeOverride ?? queryModel.TakeCount;

        builder.Append(Select);

        if (queryModel.IsDistinct)
        {
            builder.Append(" DISTINCT");
        }

        if (queryModel.SkipCount is null && effectiveTake is int takeCount)
        {
            builder.Append(" TOP (");
            builder.Append(takeCount);
            builder.Append(')');
        }
    }

    private static void AppendSelectHeadForInnerProjectedQuery(StringBuilder builder, GraphQueryModel queryModel)
    {
        builder.Append(Select);

        if (queryModel.IsDistinct)
        {
            builder.Append(" DISTINCT");
        }
    }

    private static void AppendOuterSelectHead(StringBuilder builder, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        var effectiveTake = effectiveTakeOverride ?? queryModel.TakeCount;

        builder.Append(Select);

        if (queryModel.SkipCount is null && effectiveTake is int takeCount)
        {
            builder.Append(" TOP (");
            builder.Append(takeCount);
            builder.Append(')');
        }
    }

    private static void AppendOuterSelectHeadForWrapper(StringBuilder builder, GraphQueryModel queryModel, bool preserveOrdering)
    {
        builder.Append(Select);

        if (preserveOrdering && queryModel.SkipCount is null && queryModel.TakeCount is int takeCount)
        {
            builder.Append(" TOP (");
            builder.Append(takeCount);
            builder.Append(')');
        }
    }

    private static void AppendSelectHeadForExists(StringBuilder builder, GraphQueryModel queryModel, int? effectiveTakeOverride)
    {
        var effectiveTake = effectiveTakeOverride ?? queryModel.TakeCount ?? 1;

        builder.Append(Select);

        if (queryModel.SkipCount is null)
        {
            builder.Append(" TOP (");
            builder.Append(effectiveTake);
            builder.Append(')');
        }
    }

    private static void AppendCountInnerSelectPrefix(StringBuilder builder, GraphQueryModel queryModel)
    {
        if (!queryModel.IsDistinct)
        {
            builder.Append("1 AS [CountValue], ");
        }
    }

    private static void AppendWrappedQuery(StringBuilder builder, string commandText)
    {
        builder.AppendLine("FROM (");
        builder.Append(commandText);
        builder.AppendLine(") AS q");
    }

    private static string GetDefaultOuterOrderByColumn(GraphQueryModel queryModel) => queryModel.Projection switch
    {
        GraphScalarProjection => "Value",
        GraphObjectProjection objectProjection when objectProjection.Bindings.Count > 0 => objectProjection.Bindings[0].TargetMemberName,
        GraphConstructorProjection constructorProjection when constructorProjection.Parameters.Count > 0 => constructorProjection.Parameters[0].ParameterName,
        _ => "Value"
    };
}