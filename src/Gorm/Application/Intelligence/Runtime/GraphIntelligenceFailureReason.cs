namespace Gorm.Application.Intelligence.Runtime;

/// <summary>
/// Classifies bounded runtime failures independently of provider implementation.
/// </summary>
public enum GraphIntelligenceFailureReason
{
    /// <summary>
    /// The configured execution timeout elapsed.
    /// </summary>
    TimedOut,

    /// <summary>
    /// The algorithm id, options or named parameters were invalid.
    /// </summary>
    InvalidInvocation,

    /// <summary>
    /// The selected algorithm failed during execution.
    /// </summary>
    AlgorithmFailed,

    /// <summary>
    /// The projection change feed or live projection update failed.
    /// </summary>
    ChangeFeedFailed,

    /// <summary>
    /// The configured live execution limit was reached.
    /// </summary>
    LiveExecutionLimitExceeded,

    /// <summary>
    /// An explicitly invoked output adapter failed.
    /// </summary>
    OutputAdapterFailed
}