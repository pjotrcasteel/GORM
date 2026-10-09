using System.Text.Json;

namespace Gorm.Playground.Core;

/// <summary>
/// A bounded mapped-property equality filter. Never interprets arbitrary editor C#.
/// </summary>
public sealed record PlaygroundPredicateIntent(int Version, string Root, string Property, string Value, string OrderBy, int Skip, int Take)
{
    private static readonly HashSet<string> Fields = ["version", "root", "property", "value", "orderBy", "skip", "take"];

    public static PlaygroundPredicateIntent Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > 4096)
        {
            throw new NotSupportedException("Playground predicate intent is too long.");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("The predicate intent must be a JSON object.");
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!Fields.Contains(property.Name) || !fields.TryAdd(property.Name, property.Value))
            {
                throw new NotSupportedException($"Unknown or duplicate mapped-predicate field '{property.Name}'.");
            }
        }

        if (fields.Count != Fields.Count || fields["version"].ValueKind != JsonValueKind.Number
            || !fields["version"].TryGetInt32(out var version) || fields["root"].ValueKind != JsonValueKind.String
            || fields["property"].ValueKind != JsonValueKind.String || fields["value"].ValueKind != JsonValueKind.String
            || fields["orderBy"].ValueKind != JsonValueKind.String || fields["skip"].ValueKind != JsonValueKind.Number
            || !fields["skip"].TryGetInt32(out var skip) || fields["take"].ValueKind != JsonValueKind.Number
            || !fields["take"].TryGetInt32(out var take))
        {
            throw new NotSupportedException("The v4 predicate requires exact root, property, value, orderBy, skip and take types.");
        }

        var intent = new PlaygroundPredicateIntent(version, fields["root"].GetString()!, fields["property"].GetString()!,
            fields["value"].GetString()!, fields["orderBy"].GetString()!, skip, take);

        if (intent.Version != 4 || intent.OrderBy != "Name" || intent.Skip is < 0 or > 10000
            || intent.Take is < 1 or > 100 || intent.Value.Length is < 1 or > 64
            || intent.Property == "Name" && !intent.Value.All(IsSafeNameCharacter)
            || intent.Property == "State" && intent.Value is not ("Active" or "Inactive"))
        {
            throw new NotSupportedException("Only bounded Name equality or Active/Inactive State equality is supported.");
        }

        return intent;
    }

    private static bool IsSafeNameCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is ' ' or '.' or '_' or '-';
}

public sealed record PlaygroundPredicateField(string Root, string Property, string ValueType);
