using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Live;

/// <summary>
/// Provides a bounded asynchronous single-consumer change feed per projection key for in-memory and adapter tests.
/// </summary>
public sealed class GraphChannelProjectionChangeFeed : IGraphProjectionChangeFeed
{
    private readonly ConcurrentDictionary<GraphProjectionKey, Channel<GraphProjectionChange>> _channels = [];
    private readonly int _capacity;

    /// <summary>
    /// Initializes a bounded channel change feed.
    /// </summary>
    public GraphChannelProjectionChangeFeed(int capacity = 1_024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
    }

    /// <summary>
    /// Publishes one change with bounded backpressure.
    /// </summary>
    public ValueTask PublishAsync(GraphProjectionChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        return GetChannel(change.Delta.Key).Writer.WriteAsync(change, cancellationToken);
    }

    /// <summary>
    /// Completes one logical projection feed.
    /// </summary>
    public bool Complete(GraphProjectionKey key, Exception? error = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetChannel(key).Writer.TryComplete(error);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<GraphProjectionChange> ReadChangesAsync(GraphProjectionKey key, GraphChangeFeedCursor? after = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return ReadChangesCoreAsync(key, after, cancellationToken);
    }

    private async IAsyncEnumerable<GraphProjectionChange> ReadChangesCoreAsync(
        GraphProjectionKey key,
        GraphChangeFeedCursor? after,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var minimumSequence = after?.Sequence ?? 0;
        await foreach (var change in GetChannel(key).Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (change.Cursor.Sequence > minimumSequence)
            {
                yield return change;
            }
        }
    }

    private Channel<GraphProjectionChange> GetChannel(GraphProjectionKey key) =>
        _channels.GetOrAdd(
            key,
            _ => Channel.CreateBounded<GraphProjectionChange>(new BoundedChannelOptions(_capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            }));
}