namespace Gorm.Application.Tracking;

/// <summary>
/// Represents struct.
/// </summary>
internal readonly record struct GraphRelationshipFixupKey(
    Type OwnerType,
    Guid OwnerId,
    string RelationshipName,
    bool IncludeEdge);