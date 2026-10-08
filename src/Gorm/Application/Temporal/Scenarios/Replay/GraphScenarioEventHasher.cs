using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Gorm.Application.Temporal.Scenarios.Replay;

internal static class GraphScenarioEventHasher
{
    public static string Calculate(GraphScenarioId scenarioId, long sequence, string? previousEventId, GraphScenarioMutation mutation)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> number = stackalloc byte[sizeof(long)];
        AppendString(hash, scenarioId.Value);
        BinaryPrimitives.WriteInt64LittleEndian(number, sequence);
        hash.AppendData(number);
        AppendString(hash, previousEventId ?? string.Empty);
        AppendString(hash, mutation.MutationKind.ToString());
        AppendString(hash, mutation.EntityKind.ToString());
        Span<byte> identifier = stackalloc byte[16];
        mutation.EntityId.TryWriteBytes(identifier);
        hash.AppendData(identifier);
        BinaryPrimitives.WriteInt64LittleEndian(number, mutation.ValidAt.UtcTicks);
        hash.AppendData(number);
        BinaryPrimitives.WriteInt64LittleEndian(number, mutation.RecordedAt.UtcTicks);
        hash.AppendData(number);
        AppendString(hash, mutation.Description);

        if (mutation.NodeState is not null)
        {
            AppendString(hash, mutation.NodeState.EntityType.FullName ?? mutation.NodeState.EntityType.Name);
            hash.AppendData(mutation.NodeState.Payload);
        }

        if (mutation.EdgeState is not null)
        {
            AppendString(hash, mutation.EdgeState.EntityType.FullName ?? mutation.EdgeState.EntityType.Name);
            hash.AppendData(mutation.EdgeState.Payload);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}