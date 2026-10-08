using System.Data;
using System.Data.Common;

namespace Gorm.Infrastructure.Sql;

/// <summary>
/// Creates database commands for generated GORM SQL.
/// </summary>
public static class GraphSqlCommandFactory
{
    /// <summary>
    /// Creates a database command for the generated graph SQL.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="sql">The generated SQL.</param>
    /// <returns>The command.</returns>
    public static DbCommand CreateCommand(DbConnection connection, GraphSqlQuery sql) =>
        CreateCommand(connection, sql, transaction: null);

    /// <summary>
    /// Creates a database command for the generated graph SQL.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="sql">The generated SQL.</param>
    /// <param name="transaction">The active transaction.</param>
    /// <returns>The command.</returns>
    public static DbCommand CreateCommand(DbConnection connection, GraphSqlQuery sql, DbTransaction? transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);

        var command = connection.CreateCommand();
        command.CommandText = sql.CommandText;
        command.CommandType = CommandType.Text;
        command.Transaction = transaction;

        AddParameters(command, sql.Parameters);

        return command;
    }

    /// <summary>
    /// Adds parameters to a database command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="parameters">The parameters.</param>
    public static void AddParameters(DbCommand command, IList<GraphSqlParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parameters);

        for (var i = 0; i < parameters.Count; i++)
        {
            AddParameter(command, parameters[i]);
        }
    }

    /// <summary>
    /// Adds a parameter to a database command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    public static void AddParameter(DbCommand command, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        AddParameterCore(command, name, value, dbType: null, size: null, inferMetadata: true);
    }

    /// <summary>
    /// Adds a parameter to a database command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The parameter.</param>
    public static void AddParameter(DbCommand command, GraphSqlParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parameter);

        AddParameterCore(command, parameter.Name, parameter.Value, parameter.DbType, parameter.Size, inferMetadata: false);
    }

    private static void AddParameterCore(DbCommand command, string name, object? value, DbType? dbType, int? size, bool inferMetadata)
    {
        var dbParameter = command.CreateParameter();

        dbParameter.ParameterName = name;
        dbParameter.Value = NormalizeValue(value) ?? DBNull.Value;

        if (inferMetadata)
        {
            ApplyInferredMetadata(dbParameter, value);
        }
        else
        {
            ApplyExplicitMetadata(dbParameter, dbType, size);
        }

        command.Parameters.Add(dbParameter);
    }

    private static void ApplyExplicitMetadata(DbParameter parameter, DbType? dbType, int? size)
    {
        if (dbType is { } effectiveDbType)
        {
            parameter.DbType = effectiveDbType;
        }

        if (size is { } effectiveSize)
        {
            parameter.Size = effectiveSize;
        }
    }

    private static void ApplyInferredMetadata(DbParameter parameter, object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                return;

            case string text:
                parameter.DbType = DbType.String;
                parameter.Size = text.Length <= 4000 ? Math.Max(text.Length, 1) : -1;
                return;

            case byte[] bytes:
                parameter.DbType = DbType.Binary;
                parameter.Size = bytes.Length <= 8000 ? Math.Max(bytes.Length, 1) : -1;
                return;

            case Guid:
                parameter.DbType = DbType.Guid;
                return;

            case int:
                parameter.DbType = DbType.Int32;
                return;

            case long:
                parameter.DbType = DbType.Int64;
                return;

            case short:
                parameter.DbType = DbType.Int16;
                return;

            case byte:
                parameter.DbType = DbType.Byte;
                return;

            case bool:
                parameter.DbType = DbType.Boolean;
                return;

            case DateTime:
                parameter.DbType = DbType.DateTime2;
                return;

            case DateTimeOffset:
                parameter.DbType = DbType.DateTimeOffset;
                return;

            case DateOnly:
                parameter.DbType = DbType.Date;
                return;

            case TimeOnly:
            case TimeSpan:
                parameter.DbType = DbType.Time;
                return;

            case decimal:
                parameter.DbType = DbType.Decimal;
                return;

            case double:
                parameter.DbType = DbType.Double;
                return;

            case float:
                parameter.DbType = DbType.Single;
                return;
        }
    }

    private static object? NormalizeValue(object? value) =>
        value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time => time.ToTimeSpan(),
            _ => value
        };
}