using System.Text.Json;

namespace Gorm.Playground.Core;

/// <summary>
/// A strict versioned two-predicate AND filter over mapped service Name and State.
/// Never accepts executable C# or arbitrary JSON query trees.
/// </summary>
public sealed record PlaygroundCombinedPredicateIntent(int Version, string Root, string Name, string State, string Logic, string OrderBy, int Skip, int Take)
{
    private static readonly HashSet<string> Fields = ["version", "root", "name", "state", "logic", "orderBy", "skip", "take"];

    public static PlaygroundCombinedPredicateIntent Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > 4096)
        {
            throw new NotSupportedException("Combined filter intent is too large.");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException("Combined filter intent must be a JSON object.");
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!Fields.Contains(property.Name) || !fields.TryAdd(property.Name, property.Value))
            {
                throw new NotSupportedException($"Unknown or duplicate combined filter field '{property.Name}'.");
            }
        }

        if (fields.Count != Fields.Count || fields["version"].ValueKind != JsonValueKind.Number
            || !fields["version"].TryGetInt32(out var version)
            || fields["root"].ValueKind != JsonValueKind.String || fields["name"].ValueKind != JsonValueKind.String
            || fields["state"].ValueKind != JsonValueKind.String || fields["logic"].ValueKind != JsonValueKind.String
            || fields["orderBy"].ValueKind != JsonValueKind.String
            || fields["skip"].ValueKind != JsonValueKind.Number || !fields["skip"].TryGetInt32(out var skip)
            || fields["take"].ValueKind != JsonValueKind.Number || !fields["take"].TryGetInt32(out var take))
        {
            throw new NotSupportedException("Combined filter requires exact version, root, name, state, logic, orderBy, skip and take types.");
        }

        var intent = new PlaygroundCombinedPredicateIntent(version, fields["root"].GetString()!, fields["name"].GetString()!,
            fields["state"].GetString()!, fields["logic"].GetString()!, fields["orderBy"].GetString()!, skip, take);

        if (intent.Version != 5 || intent.Root != "ServiceNode" || intent.Logic != "and"
            || intent.OrderBy != "Name" || intent.State is not ("Active" or "Inactive")
            || intent.Skip is < 0 or > 10000 || intent.Take is < 1 or > 100
            || intent.Name.Length is < 1 or > 64 || !intent.Name.All(IsSafeCharacter))
        {
            throw new NotSupportedException("Only Name AND State equality on mapped services, ordered by Name with bounded paging, is supported.");
        }

        return intent;
    }

    private static bool IsSafeCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is ' ' or '.' or '_' or '-';
}
