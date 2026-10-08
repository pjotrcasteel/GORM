namespace Gorm.Application.Intelligence.Execution;

/// <summary>
/// Represents one deterministic contiguous zero-based work range.
/// </summary>
public readonly record struct GraphWorkPartition(int Index, int StartIndex, int Length)
{
    /// <summary>
    /// Gets the exclusive end index.
    /// </summary>
    public int EndIndex => StartIndex + Length;
}