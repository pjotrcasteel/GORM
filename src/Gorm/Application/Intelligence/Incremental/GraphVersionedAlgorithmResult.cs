using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Incremental;

/// <summary>
/// Associates an algorithm result with the exact projection key and source version that produced it.
/// </summary>
public sealed class GraphVersionedAlgorithmResult<TResult>
{
    internal GraphVersionedAlgorithmResult(string algorithmName, GraphProjectionKey key, long version, TResult result, object? incrementalState)
    {
        AlgorithmName = algorithmName;
        Key = key;
        Version = version;
        Result = result;
        IncrementalState = incrementalState;
    }

    /// <summary>
    /// Gets the stable algorithm identifier.
    /// </summary>
    public string AlgorithmName { get; }

    /// <summary>
    /// Gets the logical projection key.
    /// </summary>
    public GraphProjectionKey Key { get; }

    /// <summary>
    /// Gets the exact source version used by the algorithm.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the algorithm result.
    /// </summary>
    public TResult Result { get; }

    internal object? IncrementalState { get; }
}