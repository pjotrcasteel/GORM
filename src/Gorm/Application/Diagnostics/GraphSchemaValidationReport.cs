using System.Text;

namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph schema validation report.
/// </summary>
public sealed class GraphSchemaValidationReport
{
    /// <summary>
    /// Gets the validation issues that were found.
    /// </summary>
    public required IReadOnlyList<GraphSchemaValidationIssue> Issues { get; init; }

    /// <summary>
    /// Gets the error issues.
    /// </summary>
    public IEnumerable<GraphSchemaValidationIssue> Errors => Issues.Where(x => x.Severity == GraphSchemaValidationSeverity.Error);

    /// <summary>
    /// Gets the warning issues.
    /// </summary>
    public IEnumerable<GraphSchemaValidationIssue> Warnings => Issues.Where(x => x.Severity == GraphSchemaValidationSeverity.Warning);

    /// <summary>
    /// Gets a value indicating whether the schema is valid.
    /// </summary>
    public bool IsValid => !Errors.Any();

    /// <summary>
    /// Throws when the report contains errors.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (IsValid)
        {
            return;
        }

        throw new InvalidOperationException(ToString());
    }

    /// <summary>
    /// Formats the validation report.
    /// </summary>
    /// <returns>The report text.</returns>
    public override string ToString()
    {
        if (Issues.Count == 0)
        {
            return "Graph schema is valid.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Graph schema validation found {Issues.Count} issue(s):");

        foreach (var issue in Issues)
        {
            builder.Append("- ");
            builder.Append(issue.Severity);
            builder.Append(' ');
            builder.Append(issue.Code);
            builder.Append(": ");
            builder.Append(issue.Message);

            if (!string.IsNullOrWhiteSpace(issue.Expected) || !string.IsNullOrWhiteSpace(issue.Actual))
            {
                builder.Append(" Expected='");
                builder.Append(issue.Expected ?? "<none>");
                builder.Append("', Actual='");
                builder.Append(issue.Actual ?? "<none>");
                builder.Append('\'');
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}