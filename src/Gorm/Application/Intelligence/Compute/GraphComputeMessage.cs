namespace Gorm.Application.Intelligence.Compute;

internal readonly record struct GraphComputeMessage<TMessage>(int TargetNodeIndex, TMessage Value);