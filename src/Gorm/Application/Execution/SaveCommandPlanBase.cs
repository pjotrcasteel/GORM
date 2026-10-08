namespace Gorm.Application.Execution;

internal abstract class SaveCommandPlanBase
{
    public required Type ClrType { get; init; }
    public required string KeyPropertyName { get; init; }
    public required GraphSavePropertyPlan KeyProperty { get; init; }
    public required GraphSavePropertyPlan[] InsertProperties { get; init; }
    public required GraphSavePropertyPlan[] UpdatableProperties { get; init; }
    public required GraphSavePropertyPlan[] NonConcurrencyUpdatableProperties { get; init; }
    public required GraphSavePropertyPlan? ConcurrencyProperty { get; init; }
    public required string[] InsertParameterNames { get; init; }
    public required string[] UpdateWithoutConcurrencyParameterNames { get; init; }
    public required string[] UpdateWithConcurrencyParameterNames { get; init; }
    public required string[] DeleteWithoutConcurrencyParameterNames { get; init; }
    public required string[] DeleteWithConcurrencyParameterNames { get; init; }
    public required string InsertCommandText { get; init; }
    public required string UpdateWithoutConcurrencyCommandText { get; init; }
    public required string? UpdateWithConcurrencyCommandText { get; init; }
    public required string DeleteWithoutConcurrencyCommandText { get; init; }
    public required string? DeleteWithConcurrencyCommandText { get; init; }
}