using System.Globalization;

namespace Gorm.Infrastructure.Providers.SqlServer.Helpers;

/// <summary>
/// Represents sql generation helpers.
/// </summary>
public static class SqlGenerationHelpers
{
    /// <summary>
    /// SQL Server LIKE escape character used for generated predicates.
    /// </summary>
    public const char LikeEscapeCharacter = '~';

    /// <summary>
    /// SQL Server LIKE ESCAPE clause used for generated predicates.
    /// </summary>
    public const string LikeEscapeSql = "ESCAPE '~'";

    /// <summary>
    /// Escapes a SQL Server identifier with square brackets.
    /// </summary>
    /// <param name="name">The identifier name.</param>
    /// <returns>The escaped identifier.</returns>
    public static string Escape(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("SQL identifier cannot be null or whitespace.", nameof(name));
        }

        return $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    /// <summary>
    /// Escapes a SQL Server schema-qualified object name.
    /// </summary>
    /// <param name="schema">The schema name.</param>
    /// <param name="tableName">The table name.</param>
    /// <returns>The escaped two-part object name.</returns>
    public static string EscapeFullName(string schema, string tableName) => $"{Escape(schema)}.{Escape(tableName)}";

    /// <summary>
    /// Escapes a SQL Server column reference with an already known alias and column name.
    /// </summary>
    /// <param name="alias">The table alias.</param>
    /// <param name="columnName">The column name.</param>
    /// <returns>The escaped column reference.</returns>
    public static string EscapeColumn(string alias, string columnName) => $"{Escape(alias)}.{Escape(columnName)}";

    /// <summary>
    /// Escapes a SQL Server LIKE pattern so %, _, [, and the escape character are treated as literal input.
    /// </summary>
    /// <param name="value">The user value used inside the LIKE pattern.</param>
    /// <returns>The escaped LIKE pattern value.</returns>
    public static string EscapeLikePattern(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Replace(LikeEscapeCharacter.ToString(CultureInfo.InvariantCulture), "~~", StringComparison.Ordinal)
            .Replace("%", "~%", StringComparison.Ordinal)
            .Replace("_", "~_", StringComparison.Ordinal)
            .Replace("[", "~[", StringComparison.Ordinal);
    }

    /// <summary>
    /// Escapes a SQL string literal value without adding the surrounding quotes.
    /// </summary>
    /// <param name="value">The literal value.</param>
    /// <returns>The escaped literal value.</returns>
    public static string EscapeLiteral(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}