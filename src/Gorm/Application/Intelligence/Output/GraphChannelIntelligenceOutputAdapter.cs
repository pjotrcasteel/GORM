using System.Threading.Channels;
using Gorm.Application.Intelligence.Registry;

namespace Gorm.Application.Intelligence.Output;

/// <summary>
/// Provides an explicit bounded in-process result stream with asynchronous backpressure.
/// </summary>
public sealed class GraphChannelIntelligenceOutputAdapter : IGraphIntelligenceOutputAdapter
{
    private readonly Channel<GraphAlgorithmExecutionResult> _channel;

    /// <summary>
    /// Initializes a bounded single-consumer output stream.
    /// </summary>
    public GraphChannelIntelligenceOutputAdapter(string id, int capacity = 256)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An output adapter identifier cannot be empty.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Id = id;
        _channel = Channel.CreateBounded<GraphAlgorithmExecutionResult>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public ValueTask WriteAsync(GraphAlgorithmExecutionResult execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return _channel.Writer.WriteAsync(execution, cancellationToken);
    }

    /// <summary>
    /// Reads results after callers have explicitly dispatched them.
    /// </summary>
    public IAsyncEnumerable<GraphAlgorithmExecutionResult> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Completes the output stream.
    /// </summary>
    public bool Complete(Exception? error = null) => _channel.Writer.TryComplete(error);
}