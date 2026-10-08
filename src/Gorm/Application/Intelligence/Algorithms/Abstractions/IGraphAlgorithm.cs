using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Algorithms.Abstractions;

/// <summary>
/// Defines an algorithm that runs against a detached graph projection.
/// </summary>
public interface IGraphAlgorithm<out TResult>
{
    /// <summary>
    /// Executes the algorithm.
    /// </summary>
    public TResult Execute(GraphProjection projection, CancellationToken cancellationToken = default);
}