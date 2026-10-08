using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Registry;

/// <summary>
/// Wraps every registry result with invocation and exact snapshot provenance.
/// </summary>
public sealed class GraphAlgorithmExecutionResult
{
    /// <summary>
    /// Gets the invocation correlation identifier.
    /// </summary>
    public required Guid InvocationId { get; init; }

    /// <summary>
    /// Gets the stable registered algorithm identifier.
    /// </summary>
    public required string AlgorithmId { get; init; }

    /// <summary>
    /// Gets the immutable projection metadata against which the algorithm ran.
    /// </summary>
    public required GraphProjectionSnapshotMetadata Snapshot { get; init; }

    /// <summary>
    /// Gets the UTC execution start.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets the UTC execution completion.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Gets elapsed execution time.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the declared result type.
    /// </summary>
    public required Type ResultType { get; init; }

    /// <summary>
    /// Gets the algorithm result. The registry never persists it implicitly.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets the result as its expected type.
    /// </summary>
    public TResult? AsResult<TResult>()
    {
        if (Result is null)
        {
            return default;
        }

        return Result is TResult typed
            ? typed
            : throw new InvalidCastException($"Algorithm '{AlgorithmId}' returned '{Result.GetType().FullName}', not '{typeof(TResult).FullName}'.");
    }
}