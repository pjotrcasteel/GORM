using System.Text;

namespace Gorm.Application.Mutations.Validations;

/// <summary>
/// Represents the validation result of a graph mutation.
/// </summary>
public sealed class GraphMutationValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMutationValidationResult"/> class.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public GraphMutationValidationResult(IReadOnlyList<GraphMutationValidationError> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    /// <summary>
    /// Gets a value indicating whether the validation result is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<GraphMutationValidationError> Errors { get; }

    /// <summary>
    /// Converts validation errors to a readable message.
    /// </summary>
    /// <returns>The message.</returns>
    public string ToMessage()
    {
        if (IsValid)
        {
            return "Graph mutation is valid.";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Graph mutation is invalid.");

        foreach (var error in Errors)
        {
            builder.Append("- ");
            builder.Append(error.Code);
            builder.Append(": ");
            builder.Append(error.Message);

            if (!string.IsNullOrWhiteSpace(error.Path))
            {
                builder.Append(" Path: ");
                builder.Append(error.Path);
            }

            if (!string.IsNullOrWhiteSpace(error.Key))
            {
                builder.Append(" Key: ");
                builder.Append(error.Key);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}