using Gorm.Application.Context;

namespace Gorm.Application.Diagnostics;

/// <summary>
/// Represents graph context diagnostics extensions.
/// </summary>
public static class GraphContextDiagnosticsExtensions
{
    /// <summary>
    /// Validates the graph schema.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<GraphSchemaValidationReport> ValidateSchemaAsync(this GraphContext context, CancellationToken cancellationToken = default) =>
        GraphSchemaValidator.ValidateAsync(context, cancellationToken);

    /// <summary>
    /// Validates the graph schema.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="options">The validation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<GraphSchemaValidationReport> ValidateSchemaAsync(
        this GraphContext context,
        GraphSchemaValidationOptions options,
        CancellationToken cancellationToken = default) =>
        GraphSchemaValidator.ValidateAsync(context, options, cancellationToken);

    /// <summary>
    /// Executes assert valid schema async.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task AssertValidSchemaAsync(this GraphContext context, CancellationToken cancellationToken = default) =>
        AssertValidSchemaAsync(context, new GraphSchemaValidationOptions(), cancellationToken);

    /// <summary>
    /// Executes assert valid schema async.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="options">The validation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task AssertValidSchemaAsync(this GraphContext context, GraphSchemaValidationOptions options, CancellationToken cancellationToken = default)
    {
        var report = await context.ValidateSchemaAsync(options, cancellationToken).ConfigureAwait(false);
        report.ThrowIfInvalid();
    }
}