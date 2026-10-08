using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Serializes and integrity-checks stable explainable twin reports.
/// </summary>
public static class GraphTwinReportSerializer
{
    /// <summary>
    /// Serializes a report.
    /// </summary>
    public static string Serialize(GraphTwinEvidenceReport report, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, Options(indented));
    }

    /// <summary>
    /// Deserializes and verifies content integrity.
    /// </summary>
    public static GraphTwinEvidenceReport DeserializeAndVerify(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Report JSON is required.", nameof(json));
        }

        var report = JsonSerializer.Deserialize<GraphTwinEvidenceReport>(json, Options(false)) ?? throw new InvalidOperationException("Twin report JSON did not contain a report.");
        if (!string.Equals(report.ReportFingerprint, Fingerprint(report.Content), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Twin report fingerprint does not match its content.");
        }

        return report;
    }

    internal static string Fingerprint(GraphTwinReportContent content)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(content, Options(false));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static JsonSerializerOptions Options(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}