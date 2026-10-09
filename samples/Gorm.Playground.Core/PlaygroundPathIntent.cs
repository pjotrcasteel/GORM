using System.Text.Json;

namespace Gorm.Playground.Core;

/// <summary>A strictly validated, bounded two-hop path intent; never arbitrary C# source.</summary>
public sealed record PlaygroundPathIntent(int Version, string Root, Guid NodeId, IReadOnlyList<PlaygroundPathHop> Hops)
{
    private static readonly HashSet<string> Fields = ["version", "root", "nodeId", "hops"];
    private static readonly HashSet<string> HopFields = ["direction", "edge", "target"];

    public static PlaygroundPathIntent Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > 4096)
        {
            throw new NotSupportedException("The Playground path intent exceeds the allowed length.");
        }

        using var document = JsonDocument.Parse(json);
        var fields = ReadFields(document.RootElement, Fields);
        if (fields.Count != 4 || fields["version"].ValueKind != JsonValueKind.Number
            || !fields["version"].TryGetInt32(out var version) || version != 3
            || fields["root"].ValueKind != JsonValueKind.String || fields["nodeId"].ValueKind != JsonValueKind.String
            || !Guid.TryParseExact(fields["nodeId"].GetString(), "D", out var nodeId)
            || fields["hops"].ValueKind != JsonValueKind.Array || fields["hops"].GetArrayLength() != 2)
        {
            throw new NotSupportedException("A v3 path requires root, nodeId and exactly two graph hops with exact types.");
        }

        var hops = new List<PlaygroundPathHop>(2);
        foreach (var hop in fields["hops"].EnumerateArray())
        {
            var values = ReadFields(hop, HopFields);
            if (values.Count != 3 || values["direction"].ValueKind != JsonValueKind.String
                || values["edge"].ValueKind != JsonValueKind.String || values["target"].ValueKind != JsonValueKind.String)
            {
                throw new NotSupportedException("Each hop must contain string direction, edge and target fields.");
            }

            var direction = values["direction"].GetString()!;
            if (direction is not ("outgoing" or "incoming"))
            {
                throw new NotSupportedException("Only outgoing and incoming graph hops are supported.");
            }

            hops.Add(new PlaygroundPathHop(direction, values["edge"].GetString()!, values["target"].GetString()!));
        }

        return new PlaygroundPathIntent(version, fields["root"].GetString()!, nodeId, hops);
    }

    private static Dictionary<string, JsonElement> ReadFields(JsonElement value, HashSet<string> allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("A Playground path and each hop must be JSON objects.");
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject())
        {
            if (!allowed.Contains(field.Name) || !result.TryAdd(field.Name, field.Value))
            {
                throw new NotSupportedException($"Unsupported or duplicate Playground field '{field.Name}'.");
            }
        }

        return result;
    }
}

public sealed record PlaygroundPathHop(string Direction, string Edge, string Target);

public sealed record PlaygroundPathRoute(string Root, IReadOnlyList<PlaygroundPathHop> Hops);
