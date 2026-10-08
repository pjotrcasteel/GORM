using System.Text.RegularExpressions;
using Gorm.Demo.Infrastructure;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Gorm.SqlServer.Tests;

[TestClass]
public sealed partial class SqlServerGraphIntegrationTests
{
    private static MsSqlContainer? _container;
    private static string _databaseConnectionString = string.Empty;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext context)
    {
        // Each test run owns a disposable SQL Server 2022 instance. Never connect to an existing developer database.
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync(context.CancellationToken);

        var master = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        };

        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync(context.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE DATABASE [GormIntegration]";
            await command.ExecuteNonQueryAsync(context.CancellationToken);
        }

        master.InitialCatalog = "GormIntegration";
        _databaseConnectionString = master.ConnectionString;

        await using var schemaConnection = new SqlConnection(_databaseConnectionString);
        await schemaConnection.OpenAsync(context.CancellationToken);

        // GORM is database-first. A fixture applies the DBA-style SQL supplied with the demo.
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "001_schema.sql");
        var schemaScript = await File.ReadAllTextAsync(schemaPath, context.CancellationToken);
        foreach (var batch in Regex.Split(schemaScript, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = schemaConnection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(context.CancellationToken);
        }

        await using var historySchema = schemaConnection.CreateCommand();
        historySchema.CommandText = """
            CREATE TABLE [dbo].[GormNodeHistory]
            (
                [HistoryId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                [EntityType] NVARCHAR(512) NOT NULL,
                [EntityId] UNIQUEIDENTIFIER NOT NULL,
                [OperationKind] INT NOT NULL,
                [CapturedAtUtc] DATETIME2 NOT NULL,
                [ValidFromUtc] DATETIME2 NOT NULL,
                [ValidToUtc] DATETIME2 NULL,
                [SnapshotJson] NVARCHAR(MAX) NOT NULL
            );
            CREATE TABLE [dbo].[GormEdgeHistory]
            (
                [HistoryId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                [EntityType] NVARCHAR(512) NOT NULL,
                [EntityId] UNIQUEIDENTIFIER NOT NULL,
                [OperationKind] INT NOT NULL,
                [CapturedAtUtc] DATETIME2 NOT NULL,
                [ValidFromUtc] DATETIME2 NOT NULL,
                [ValidToUtc] DATETIME2 NULL,
                [FromId] UNIQUEIDENTIFIER NULL,
                [ToId] UNIQUEIDENTIFIER NULL,
                [SnapshotJson] NVARCHAR(MAX) NOT NULL
            );
            """;
        await historySchema.ExecuteNonQueryAsync(context.CancellationToken);
    }

    [ClassCleanup]
    public static async Task CleanupAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static SqlSpecificationGraphContext CreateContext()
    {
        var context = new SqlSpecificationGraphContext();
        context.UseConnectionFactory(new SqlConnectionFactory(new GormSqlServerOptions { ConnectionString = _databaseConnectionString }));
        return context;
    }
}
