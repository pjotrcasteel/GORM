namespace Gorm.Application.Temporal.Operations;

/// <summary>
/// Publishes the stable Temporal Digital Twin product contract.
/// </summary>
public static class GraphTemporalProductContract
{
    /// <summary>
    /// Gets the stable major product API version.
    /// </summary>
    public const string ApiVersion = "3.0";

    /// <summary>
    /// Gets the stable explainable report schema.
    /// </summary>
    public const string ReportSchemaVersion = "gorm.temporal.report/1";

    /// <summary>
    /// Gets behavioural guarantees that remain part of the major-3 contract.
    /// </summary>
    public static IReadOnlyList<string> Guarantees { get; } =
    [
        "Ordinary GORM entities and provider-neutral projections are the public data boundary.",
        "Temporal analysis, scenarios, simulation and live operations never persist implicitly.",
        "Identical world evidence, options and random seed produce deterministic results.",
        "Potentially expensive operations expose cancellation and explicit safety bounds.",
        "Valid time, recorded time, expected state, observed state and scenario state remain distinct."
    ];
}