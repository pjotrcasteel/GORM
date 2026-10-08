using System.Globalization;
using Gorm.Application.Execution;
using Gorm.Application.Querying;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;
using Gorm.Demo.Infrastructure;

namespace Gorm.Demo.Features;

/// <summary>
/// Represents seed demo data.
/// </summary>
public static class SeedDemoData
{
    /// <summary>
    /// Executes parse.
    /// </summary>
    /// <returns>The value.</returns>
    public static readonly Guid InternetSpeedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", CultureInfo.InvariantCulture);
    /// <summary>
    /// Executes parse.
    /// </summary>
    /// <returns>The value.</returns>
    public static readonly Guid DownloadSpeedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", CultureInfo.InvariantCulture);
    /// <summary>
    /// Executes parse.
    /// </summary>
    /// <returns>The value.</returns>
    public static readonly Guid UploadSpeedId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc", CultureInfo.InvariantCulture);
    /// <summary>
    /// Executes parse.
    /// </summary>
    /// <returns>The value.</returns>
    public static readonly Guid SpeedTierId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Runs the operation.
    /// </summary>
    /// <param name="db">The graph context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task RunAsync(SqlSpecificationGraphContext db, CancellationToken cancellationToken = default)
    {
        var internetSpeed = await EnsureNodeAsync(
            db,
            InternetSpeedId,
            "Internet speed",
            """
            {"type":"number","unit":"Mbps","allowedValues":[100,250,500,1000]}
            """,
            cancellationToken);

        var downloadSpeed = await EnsureNodeAsync(
            db,
            DownloadSpeedId,
            "Download speed",
            """
            {"type":"number","unit":"Mbps","derivedFrom":"internet-speed"}
            """,
            cancellationToken);

        var uploadSpeed = await EnsureNodeAsync(
            db,
            UploadSpeedId,
            "Upload speed",
            """
            {"type":"number","unit":"Mbps","derivedFrom":"internet-speed"}
            """,
            cancellationToken);

        var speedTier = await EnsureNodeAsync(
            db,
            SpeedTierId,
            "Speed tier",
            """
            {"type":"string","allowedValues":["standard","premium"]}
            """,
            cancellationToken);

        await EnsureMapAsync(
            db,
            internetSpeed,
            downloadSpeed,
            """{"kind":"derived","formula":"download = internet-speed"}""",
            cancellationToken);

        await EnsureMapAsync(
            db,
            internetSpeed,
            uploadSpeed,
            """{"kind":"derived","formula":"upload = internet-speed / 2"}""",
            cancellationToken);

        await EnsureMapAsync(
            db,
            downloadSpeed,
            speedTier,
            """{"kind":"classification","formula":"download -> speed-tier"}""",
            cancellationToken);

        await EnsureMapAsync(
            db,
            uploadSpeed,
            speedTier,
            """{"kind":"classification","formula":"upload -> speed-tier"}""",
            cancellationToken);
    }

    private static async Task<CharacteristicSpecificationNode> EnsureNodeAsync(
        SqlSpecificationGraphContext db,
        Guid id,
        string name,
        string payload,
        CancellationToken cancellationToken)
    {
        var existing = await db.CharacteristicSpecifications.Where(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var changed = false;

            if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
            {
                existing.Name = name;
                changed = true;
            }

            if (!string.Equals(existing.Payload, payload, StringComparison.Ordinal))
            {
                existing.Payload = payload;
                changed = true;
            }

            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var node = new CharacteristicSpecificationNode
        {
            Id = id,
            Name = name,
            Payload = payload
        };

        db.Add(node);
        await db.SaveChangesAsync(cancellationToken);

        return node;
    }

    private static async Task EnsureMapAsync(
        SqlSpecificationGraphContext db,
        CharacteristicSpecificationNode from,
        CharacteristicSpecificationNode to,
        string payload,
        CancellationToken cancellationToken)
    {
        var existingTargetIds = await db.CharacteristicSpecifications
            .Where(x => x.Id == from.Id)
            .Outgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (existingTargetIds.Contains(to.Id))
        {
            return;
        }

        db.Connect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(
            from,
            to,
            edge =>
            {
                edge.Id = Guid.NewGuid();
                edge.Payload = payload;
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}