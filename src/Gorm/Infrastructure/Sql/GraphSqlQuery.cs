using System.Text;

namespace Gorm.Infrastructure.Sql;

/// <summary>
/// Represents graph sql query.
/// </summary>
public sealed class GraphSqlQuery
{
    /// <summary>
    /// Gets or sets the command text.
    /// </summary>
    public required string CommandText { get; init; }

    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    public IList<GraphSqlParameter> Parameters { get; } = [];

    /// <summary>
    /// Formats the command text and redacted parameter metadata for logs.
    /// </summary>
    /// <returns>The redacted command text.</returns>
    public string ToRedactedDebugString()
    {
        var builder = new StringBuilder();
        builder.AppendLine(CommandText);

        if (Parameters.Count == 0)
        {
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("-- Parameters:");

        foreach (var parameter in Parameters)
        {
            builder.Append("-- ");
            builder.Append(parameter.Name);
            builder.Append(" = ");
            builder.Append(parameter.Value is null or DBNull ? "NULL" : $"<{parameter.Value.GetType().Name}>");

            if (parameter.DbType is not null)
            {
                builder.Append(", DbType=");
                builder.Append(parameter.DbType);
            }

            if (parameter.Size is not null)
            {
                builder.Append(", Size=");
                builder.Append(parameter.Size);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}