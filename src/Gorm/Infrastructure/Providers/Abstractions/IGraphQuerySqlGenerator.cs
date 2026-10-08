using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Infrastructure.Sql;

namespace Gorm.Infrastructure.Providers.Abstractions;

/// <summary>
/// Defines i graph query sql generator.
/// </summary>
public interface IGraphQuerySqlGenerator
{
    /// <summary>
    /// Executes generate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel);

    /// <summary>
    /// Executes generate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="effectiveTakeOverride">The effective take override.</param>
    /// <returns>The value.</returns>
    public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride);

    /// <summary>
    /// Executes generate exists.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="effectiveTakeOverride">The effective take override.</param>
    /// <returns>The value.</returns>
    public GraphSqlQuery GenerateExists(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride);

    /// <summary>
    /// Executes generate aggregate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <param name="aggregateKind">The aggregate kind.</param>
    /// <returns>The value.</returns>
    public GraphSqlQuery GenerateAggregate(GraphModel model, GraphQueryModel queryModel, GraphAggregateKind aggregateKind);

    /// <summary>
    /// Executes generate count.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel);

    /// <summary>
    /// Executes generate long count.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="queryModel">The query model.</param>
    /// <returns>The value.</returns>
    public GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel);
}