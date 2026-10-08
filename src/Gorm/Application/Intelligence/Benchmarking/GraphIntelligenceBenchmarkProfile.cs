namespace Gorm.Application.Intelligence.Benchmarking;

/// <summary>
/// Defines a reproducible synthetic graph size for intelligence performance baselines.
/// </summary>
public sealed record GraphIntelligenceBenchmarkProfile
{
    /// <summary>
    /// Initializes a benchmark profile.
    /// </summary>
    public GraphIntelligenceBenchmarkProfile(string name, int nodeCount, int edgeCount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A benchmark profile name cannot be empty.", nameof(name));
        }

        if (nodeCount <= 0 || nodeCount > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount), "Benchmark node count must be between 1 and 1,000,000.");
        }

        if (edgeCount < 0 || edgeCount > 10_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeCount), "Benchmark edge count must be between 0 and 10,000,000.");
        }

        Name = name.Trim();
        NodeCount = nodeCount;
        EdgeCount = edgeCount;
    }

    /// <summary>
    /// Gets the standard small baseline profile.
    /// </summary>
    public static GraphIntelligenceBenchmarkProfile Small { get; } = new("small", 1_000, 5_000);

    /// <summary>
    /// Gets the standard medium baseline profile.
    /// </summary>
    public static GraphIntelligenceBenchmarkProfile Medium { get; } = new("medium", 10_000, 50_000);

    /// <summary>
    /// Gets the standard large baseline profile.
    /// </summary>
    public static GraphIntelligenceBenchmarkProfile Large { get; } = new("large", 100_000, 500_000);

    /// <summary>
    /// Gets the profile name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the number of generated nodes.
    /// </summary>
    public int NodeCount { get; }

    /// <summary>
    /// Gets the number of generated directed edges.
    /// </summary>
    public int EdgeCount { get; }
}