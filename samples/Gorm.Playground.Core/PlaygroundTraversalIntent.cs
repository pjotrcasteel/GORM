using System.Text.Json;

namespace Gorm.Playground.Core;

/// <summary>Single-hop, model-validated intent. The browser never submits executable C#.</summary>
public sealed record PlaygroundTraversalIntent(int Version, string Root, string Direction, string Edge, string Target, Guid NodeId)
{
    private static readonly HashSet<string> Fields = ["version", "root", "direction", "edge", "target", "nodeId"];

    public static PlaygroundTraversalIntent Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("A traversal intent must be a JSON object.");
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!Fields.Contains(property.Name) || !fields.TryAdd(property.Name, property.Value))
            {
                throw new NotSupportedException($"Unknown or duplicate traversal field '{property.Name}'.");
            }
        }

        if (fields.Count != Fields.Count || fields["version"].ValueKind != JsonValueKind.Number
            || !fields["version"].TryGetInt32(out var version)
            || fields["root"].ValueKind != JsonValueKind.String || fields["direction"].ValueKind != JsonValueKind.String
            || fields["edge"].ValueKind != JsonValueKind.String || fields["target"].ValueKind != JsonValueKind.String
            || fields["nodeId"].ValueKind != JsonValueKind.String
            || !Guid.TryParseExact(fields["nodeId"].GetString(), "D", out var nodeId))
        {
            throw new NotSupportedException("Traversal requires version, root, direction, edge, target and a valid Guid nodeId.");
        }

        var intent = new PlaygroundTraversalIntent(version, fields["root"].GetString()!, fields["direction"].GetString()!,
            fields["edge"].GetString()!, fields["target"].GetString()!, nodeId);

        if (intent.Version != 2 || intent.Direction is not ("outgoing" or "incoming"))
        {
            throw new NotSupportedException("Only v2 single-hop incoming/outgoing traversal intents are supported.");
        }

        return intent;
    }
}

public sealed record PlaygroundTraversalRoute(string Root, string Direction, string Edge, string Target);
