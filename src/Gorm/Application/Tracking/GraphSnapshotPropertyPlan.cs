namespace Gorm.Application.Tracking;

internal sealed record GraphSnapshotPropertyPlan(string Name, Func<object, object?> Get);