using Gorm.Application.Diagnostics;
using Gorm.Demo.Features;
using Gorm.Demo.Infrastructure;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;
using Microsoft.Extensions.Configuration;

namespace Gorm.Demo;

/// <summary>
/// Represents program.
/// </summary>
public static class Program
{
    /// <summary>
    /// Executes main.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task Main()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("GORM_SQLSERVER_CONNECTION") ?? configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Connection string 'Default' or GORM_SQLSERVER_CONNECTION is required.");
            return;
        }

        var connectionFactory = new SqlConnectionFactory(new GormSqlServerOptions
        {
            ConnectionString = connectionString
        });

        var db = new SqlSpecificationGraphContext();
        db.UseConnectionFactory(connectionFactory);

        Console.WriteLine("Gorm demo");
        Console.WriteLine("Database-first: schema is owned outside GORM; GORM validates and operates on the graph.");
        Console.WriteLine();

        try
        {
            var schemaReport = await db.ValidateSchemaAsync(new GraphSchemaValidationOptions
            {
                ValidateColumnTypes = true,
                ValidateColumnNullability = true,
                ValidateIndexes = true
            });

            if (!schemaReport.IsValid)
            {
                Console.WriteLine("Database schema drift detected. Apply the DBA-owned SQL from Scripts/001_schema.sql or your normal deployment process first.");
                Console.WriteLine(schemaReport.ToString());
                Console.WriteLine();
                Console.WriteLine("Expected schema script for review only:");
                Console.WriteLine(db.GenerateCreateScript());
                return;
            }

            await RunQuerySamples.RunAsync(db);

            Console.WriteLine();
            Console.WriteLine("Demo completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Demo failed:");
            Console.WriteLine(ex.ToString());
        }
    }
}