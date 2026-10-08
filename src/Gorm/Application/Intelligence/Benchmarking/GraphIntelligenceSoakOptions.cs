namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Configures a bounded dependency-free live Intelligence soak run.
/// </summary>
public sealed class GraphIntelligenceSoakOptions
{
    /// <summary>
    /// Gets or sets the initial ring graph node count.
    /// </summary>
    public int NodeCount { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the number of ordered live changes and algorithm executions.
    /// </summary>
    public int Iterations { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets bounded change-feed capacity used to exercise backpressure.
    /// </summary>
    public int FeedCapacity { get; set; } = 64;

    /// <summary>
    /// Gets or sets the interval for explicit output dispatch. Zero disables dispatch.
    /// </summary>
    public int OutputDispatchInterval { get; set; } = 100;

    /// <summary>
    /// Gets or sets the timeout for each soak algorithm execution.
    /// </summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}