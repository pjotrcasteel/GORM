namespace Gorm.Application.Intelligence.Execution;

/// <summary>
/// Creates stable contiguous partitions independent of runtime thread scheduling.
/// </summary>
public static class GraphDeterministicPartitioner
{
    /// <summary>
    /// Divides an item range into balanced contiguous partitions in ascending index order.
    /// </summary>
    public static IReadOnlyList<GraphWorkPartition> Create(int itemCount, int degreeOfParallelism, int minimumItemsPerPartition = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumItemsPerPartition);

        if (itemCount == 0)
        {
            return [];
        }

        var maximumUsefulPartitions = (itemCount + minimumItemsPerPartition - 1) / minimumItemsPerPartition;
        var partitionCount = Math.Min(degreeOfParallelism, Math.Max(maximumUsefulPartitions, 1));
        var baseLength = itemCount / partitionCount;
        var remainder = itemCount % partitionCount;
        var result = new GraphWorkPartition[partitionCount];
        var startIndex = 0;

        for (var partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
        {
            var length = baseLength + (partitionIndex < remainder ? 1 : 0);
            result[partitionIndex] = new GraphWorkPartition(partitionIndex, startIndex, length);
            startIndex += length;
        }

        return result;
    }

    internal static void Execute(IReadOnlyList<GraphWorkPartition> partitions, int degreeOfParallelism, Action<GraphWorkPartition> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        ArgumentNullException.ThrowIfNull(action);

        if (partitions.Count <= 1 || degreeOfParallelism == 1)
        {
            foreach (var partition in partitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                action(partition);
            }

            return;
        }

        Parallel.ForEach(
            partitions,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = degreeOfParallelism
            },
            action);
    }
}