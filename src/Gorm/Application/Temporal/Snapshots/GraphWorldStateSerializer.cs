using System.Text.Json;

namespace Gorm.Application.Temporal.Snapshots;

internal static class GraphWorldStateSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Serialize(object value, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(runtimeType);

        return JsonSerializer.SerializeToUtf8Bytes(value, runtimeType, SerializerOptions);
    }

    public static object Deserialize(ReadOnlySpan<byte> payload, Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        return JsonSerializer.Deserialize(payload, runtimeType, SerializerOptions)
            ?? throw new InvalidOperationException(
                $"Failed to materialize temporal world state of type '{runtimeType.FullName}'.");
    }
}