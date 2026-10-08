using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Runtime;

/// <summary>
/// Represents a classified Intelligence runtime or explicit output failure.
/// </summary>
public sealed class GraphIntelligenceRuntimeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphIntelligenceRuntimeException"/> class.
    /// </summary>
    public GraphIntelligenceRuntimeException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphIntelligenceRuntimeException"/> class.
    /// </summary>
    public GraphIntelligenceRuntimeException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphIntelligenceRuntimeException"/> class.
    /// </summary>
    public GraphIntelligenceRuntimeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a classified runtime failure.
    /// </summary>
    public GraphIntelligenceRuntimeException(
        GraphIntelligenceFailureReason reason,
        string message,
        Exception? innerException = null,
        string? algorithmId = null,
        GraphProjectionSnapshotMetadata? snapshot = null,
        string? adapterId = null)
        : base(message, innerException)
    {
        Reason = reason;
        AlgorithmId = algorithmId;
        Snapshot = snapshot;
        AdapterId = adapterId;
    }

    /// <summary>
    /// Gets the machine-readable failure classification.
    /// </summary>
    public GraphIntelligenceFailureReason Reason { get; }

    /// <summary>
    /// Gets the registry algorithm identifier, when known.
    /// </summary>
    public string? AlgorithmId { get; }

    /// <summary>
    /// Gets exact snapshot provenance, when execution had started.
    /// </summary>
    public GraphProjectionSnapshotMetadata? Snapshot { get; }

    /// <summary>
    /// Gets the explicit output adapter identifier, when relevant.
    /// </summary>
    public string? AdapterId { get; }
}