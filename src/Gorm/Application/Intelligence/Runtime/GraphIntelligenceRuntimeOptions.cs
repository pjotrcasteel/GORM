using Gorm.Application.Intelligence.Live;

namespace Gorm.Application.Intelligence.Runtime;

/// <summary>
/// Configures bounded one-shot and live Intelligence execution.
/// </summary>
public sealed class GraphIntelligenceRuntimeOptions
{
    /// <summary>
    /// Gets or sets the maximum duration of one algorithm invocation. Null disables the runtime timeout.
    /// </summary>
    public TimeSpan? ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum algorithms executed by one live subscription.
    /// </summary>
    public int MaximumLiveExecutions { get; set; } = 100_000;

    /// <summary>
    /// Gets bounded live projection options.
    /// </summary>
    public GraphLiveProjectionOptions LiveProjection { get; } = new();
}