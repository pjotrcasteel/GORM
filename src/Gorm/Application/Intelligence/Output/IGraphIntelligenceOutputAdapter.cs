using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Output;

/// <summary>
/// Defines an application-owned output target. It is invoked only by explicit dispatcher calls.
/// </summary>
public interface IGraphIntelligenceOutputAdapter
{
    /// <summary>
    /// Gets the stable adapter identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Writes one result to the explicitly selected target.
    /// </summary>
    public ValueTask WriteAsync(GraphAlgorithmExecutionResult execution, CancellationToken cancellationToken = default);
}