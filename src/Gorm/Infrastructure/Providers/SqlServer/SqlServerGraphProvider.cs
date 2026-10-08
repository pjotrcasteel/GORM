using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Infrastructure.Providers.Abstractions;
using Gorm.Infrastructure.Sql;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Represents sql server graph provider.
/// </summary>
public sealed class SqlServerGraphProvider : IGraphProvider
{
    /// <summary>
    /// Executes new.
    /// </summary>
    /// <returns>The value.</returns>
    public static SqlServerGraphProvider Instance { get; } = new();

    private SqlServerGraphProvider()
    {
        QuerySqlGenerator = new SqlServerQuerySqlGeneratorAdapter();
        SchemaGenerator = new SqlServerSchemaGeneratorAdapter();
    }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name => "SqlServerGraph";

    /// <summary>
    /// Gets or sets the query sql generator.
    /// </summary>
    public IGraphQuerySqlGenerator QuerySqlGenerator { get; }

    /// <summary>
    /// Gets or sets the schema generator.
    /// </summary>
    public IGraphSchemaGenerator SchemaGenerator { get; }

    /// <summary>
    /// Represents sql server query sql generator adapter.
    /// </summary>
    private sealed class SqlServerQuerySqlGeneratorAdapter : IGraphQuerySqlGenerator
    {
        /// <summary>
        /// Executes generate.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="queryModel">The query model.</param>
        /// <returns>The value.</returns>
        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel) => SqlServerGraphQuerySqlGenerator.Generate(model, queryModel);

        /// <summary>
        /// Executes generate.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="queryModel">The query model.</param>
        /// <param name="effectiveTakeOverride">The effective take override.</param>
        /// <returns>The value.</returns>
        public GraphSqlQuery Generate(
            GraphModel model,
            GraphQueryModel queryModel,
            int? effectiveTakeOverride) => SqlServerGraphQuerySqlGenerator.Generate(model, queryModel, effectiveTakeOverride);

        /// <summary>
        /// Executes generate exists.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="queryModel">The query model.</param>
        /// <param name="effectiveTakeOverride">The effective take override.</param>
        /// <returns>The value.</returns>
        public GraphSqlQuery GenerateExists(
            GraphModel model,
            GraphQueryModel queryModel,
            int? effectiveTakeOverride) => SqlServerGraphQuerySqlGenerator.GenerateExists(model, queryModel, effectiveTakeOverride);

        /// <summary>
        /// Executes generate aggregate.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="queryModel">The query model.</param>
        /// <param name="aggregateKind">The aggregate kind.</param>
        /// <returns>The value.</returns>
        public GraphSqlQuery GenerateAggregate(
            GraphModel model,
            GraphQueryModel queryModel,
            GraphAggregateKind aggregateKind) => SqlServerGraphQuerySqlGenerator.GenerateAggregate(model, queryModel, aggregateKind);

        /// <summary>
        /// Executes generate count.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="queryModel">The query model.</param>
        /// <returns>The value.</returns>
        public GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel) => SqlServerGraphQuerySqlGenerator.GenerateCount(model, queryModel);

        /// <summary>
        /// Executes generate long count.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="queryModel">The query model.</param>
        /// <returns>The value.</returns>
        public GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel) => SqlServerGraphQuerySqlGenerator.GenerateLongCount(model, queryModel);
    }

    /// <summary>
    /// Represents sql server schema generator adapter.
    /// </summary>
    private sealed class SqlServerSchemaGeneratorAdapter : IGraphSchemaGenerator
    {
        /// <summary>
        /// Executes generate create script.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The value.</returns>
        public string GenerateCreateScript(GraphModel model) => SqlServerGraphSchemaGenerator.GenerateCreateScript(model);
    }
}