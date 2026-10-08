namespace Gorm.Application.Intelligence.Algorithms.LinkPrediction;

/// <summary>
/// Restricts candidate pairs using an optional community result.
/// </summary>
public enum GraphLinkPredictionCandidateScope
{
    /// <summary>
    /// Evaluates pairs regardless of community membership.
    /// </summary>
    All = 0,

    /// <summary>
    /// Evaluates only pairs belonging to the same final community.
    /// </summary>
    WithinCommunity = 1,

    /// <summary>
    /// Evaluates only pairs belonging to different final communities.
    /// </summary>
    AcrossCommunities = 2
}