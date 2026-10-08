namespace Gorm.Application.Execution;

/// <summary>
/// Represents a cached save property plan.
/// </summary>
internal sealed record GraphSavePropertyPlan(string Name, string ColumnSql, Func<object, object?> Get, Action<object, object?>? Set);