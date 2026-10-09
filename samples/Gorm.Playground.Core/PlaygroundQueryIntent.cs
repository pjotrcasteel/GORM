using System.Text.Json;

namespace Gorm.Playground.Core;

/// <summary>
/// Versioned, bounded user intent. This is not arbitrary C#; only the declared graph model is accepted.
/// </summary>
public sealed record PlaygroundQueryIntent(int Version, string Root, string State, string OrderBy, int Skip, int Take)
{
    private static readonly HashSet<string> Fields = ["version", "root", "state", "orderBy", "skip", "take"];

    public static PlaygroundQueryIntent Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("Playground query intent must be a JSON object.");
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!Fields.Contains(property.Name) || !fields.TryAdd(property.Name, property.Value))
            {
                throw new NotSupportedException($"Unknown or duplicate Playground intent field: '{property.Name}'.");
            }
        }

        if (fields.Count != Fields.Count || !fields["version"].TryGetInt32(out var version)
            || fields["root"].ValueKind != JsonValueKind.String || fields["state"].ValueKind != JsonValueKind.String
            || fields["orderBy"].ValueKind != JsonValueKind.String || !fields["skip"].TryGetInt32(out var skip)
            || !fields["take"].TryGetInt32(out var take))
        {
            throw new NotSupportedException("Playground intent requires version, root, state, orderBy, skip and take with exact types.");
        }

        var intent = new PlaygroundQueryIntent(version, fields["root"].GetString()!, fields["state"].GetString()!,
            fields["orderBy"].GetString()!, skip, take);

        if (intent.Version != 1 || intent.Root != "ServiceNode" || intent.State is not ("Active" or "Inactive")
            || intent.OrderBy != "Name" || intent.Skip is < 0 or > 10000 || intent.Take is < 1 or > 100)
        {
            throw new NotSupportedException("Unsupported Playground intent: expected v1 ServiceNode, Active/Inactive state, Name order, Skip 0..10000 and Take 1..100.");
        }

        return intent;
    }
}

/// <summary>A real provider parameter, not a JavaScript-guessed placeholder.</summary>
public sealed record PlaygroundBoundParameter(string Name, object? Value, string? DbType);

/// <summary>Real GORM Explain() SQL, exact provider parameters and debug view.</summary>
public sealed record PlaygroundExplainResponse(string Sql, IReadOnlyList<PlaygroundBoundParameter> Parameters, string DebugView);
