namespace Gorm.Application.Intelligence.Incremental;

/// <summary>
/// Describes whether an algorithm update reused proven-safe prior work.
/// </summary>
public enum GraphIncrementalAlgorithmUpdateMode
{
    /// <summary>
    /// Only affected local values were recomputed.
    /// </summary>
    Incremental,

    /// <summary>
    /// The result was recalculated completely because safe reuse could not be proven.
    /// </summary>
    FullRecompute
}