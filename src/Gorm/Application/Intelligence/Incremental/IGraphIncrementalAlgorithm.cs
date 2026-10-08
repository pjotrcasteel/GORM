using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Incremental;

/// <summary>
/// Defines a version-aware graph algorithm that can prove when prior work is safe to reuse.
/// </summary>
public interface IGraphIncrementalAlgorithm<TResult>
{
    /// <summary>
    /// Gets the stable algorithm identifier.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Executes a full versioned calculation.
    /// </summary>
    public GraphVersionedAlgorithmResult<TResult> Execute(GraphProjectionSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a prior result, falling back to a full recomputation when reuse cannot be proven safe.
    /// </summary>
    public GraphIncrementalAlgorithmUpdate<TResult> Update(
        GraphVersionedAlgorithmResult<TResult> previous,
        GraphProjectionUpdateResult projectionUpdate,
        CancellationToken cancellationToken = default);
}