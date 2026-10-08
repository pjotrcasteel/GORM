namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph concurrency exception.
/// </summary>
public sealed class GraphConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphConcurrencyException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public GraphConcurrencyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphConcurrencyException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public GraphConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}