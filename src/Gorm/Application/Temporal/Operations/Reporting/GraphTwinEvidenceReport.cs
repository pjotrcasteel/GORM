namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Wraps canonical evidence content in a verifiable SHA-256 fingerprint.
/// </summary>
public sealed class GraphTwinEvidenceReport
{
    /// <summary>
    /// Gets report content.
    /// </summary>
    public required GraphTwinReportContent Content { get; init; }

    /// <summary>
    /// Gets lowercase SHA-256 over canonical content JSON.
    /// </summary>
    public required string ReportFingerprint { get; init; }

    /// <summary>
    /// Serializes the report with stable enum and property handling.
    /// </summary>
    public string ToJson(bool indented = false) => GraphTwinReportSerializer.Serialize(this, indented);
}