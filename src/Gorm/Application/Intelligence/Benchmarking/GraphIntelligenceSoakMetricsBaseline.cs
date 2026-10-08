namespace Gorm.Application.Intelligence.Benchmarking;

internal sealed record GraphIntelligenceSoakMetricsBaseline(
    long AllocatedBytes,
    long ManagedMemoryBytes,
    int Generation0Collections,
    int Generation1Collections,
    int Generation2Collections,
    long StartedAt);