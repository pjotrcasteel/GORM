namespace Gorm.Application.Temporal.Scenarios.Replay;

/// <summary>
/// Reports whether a checkpoint reproduced the expected scenario exactly.
/// </summary>
public sealed class GraphScenarioVerificationResult
{
    /// <summary>
    /// Gets whether all baseline, stream, revision and final snapshot identities match.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets the replay result when replay was attempted successfully.
    /// </summary>
    public GraphScenarioReplayResult? Replay { get; init; }

    /// <summary>
    /// Gets an explicit verification explanation.
    /// </summary>
    public required string Explanation { get; init; }
}