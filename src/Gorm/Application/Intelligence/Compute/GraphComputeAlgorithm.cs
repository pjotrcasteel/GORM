using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Application.Intelligence.Diagnostics;
using Gorm.Application.Intelligence.Execution;
using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Intelligence.Compute;

/// <summary>
/// Executes a custom vertex-centric program in deterministic synchronized supersteps.
/// </summary>
public sealed class GraphComputeAlgorithm<TState, TMessage> : IGraphAlgorithm<GraphComputeResult<TState>>
{
    private readonly IGraphComputeProgram<TState, TMessage> _program;
    private readonly GraphComputeOptions _options;
    private readonly Func<TMessage, TMessage, TMessage>? _messageReducer;

    /// <summary>
    /// Initializes a graph compute algorithm.
    /// </summary>
    public GraphComputeAlgorithm(IGraphComputeProgram<TState, TMessage> program, GraphComputeOptions? options = null, Func<TMessage, TMessage, TMessage>? messageReducer = null)
    {
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _options = options ?? new GraphComputeOptions();
        _messageReducer = messageReducer;
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public GraphComputeResult<TState> Execute(GraphProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var nodeCount = projection.Statistics.NodeCount;
        var states = InitializeStates(projection, cancellationToken);

        if (nodeCount == 0)
        {
            return new GraphComputeResult<TState>(new Dictionary<Guid, TState>(), 0, 0, converged: true);
        }

        var execution = RunSupersteps(projection, states, cancellationToken);
        return new GraphComputeResult<TState>(CreateFinalStates(projection, states), execution.Supersteps, execution.ProcessedMessages, execution.Converged);
    }

    private TState[] InitializeStates(GraphProjection projection, CancellationToken cancellationToken)
    {
        var states = new TState[projection.Statistics.NodeCount];
        for (var nodeIndex = 0; nodeIndex < states.Length; nodeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            states[nodeIndex] = _program.Initialize(projection.GetNode(nodeIndex));
        }

        return states;
    }

    private ComputeExecution RunSupersteps(GraphProjection projection, TState[] states, CancellationToken cancellationToken)
    {
        var nodeCount = projection.Statistics.NodeCount;
        var halted = new bool[nodeCount];
        var currentMessages = new List<TMessage>?[nodeCount];
        var partitions = GraphDeterministicPartitioner.Create(nodeCount, _options.DegreeOfParallelism, _options.MinimumNodesPerPartition);
        var processedMessages = 0L;
        var supersteps = 0;

        while (supersteps < _options.MaximumSupersteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = new SuperstepContext(projection, states, halted, currentMessages, partitions, cancellationToken);
            var step = ExecuteSuperstep(context, supersteps);
            processedMessages += step.ProcessedMessages;
            supersteps++;
            var hasActiveNode = halted.Any(value => !value);
            ReportProgress(supersteps, hasActiveNode, step.SentMessages);

            if (!hasActiveNode && step.SentMessages == 0)
            {
                return new ComputeExecution(supersteps, processedMessages, Converged: true);
            }

            currentMessages = step.NextMessages;
        }

        return new ComputeExecution(supersteps, processedMessages, Converged: false);
    }

    private SuperstepResult ExecuteSuperstep(SuperstepContext context, int superstep)
    {
        var nodeCount = context.Projection.Statistics.NodeCount;
        var nextMessages = new List<TMessage>?[nodeCount];
        var outgoingMessagesByNode = new List<GraphComputeMessage<TMessage>>?[nodeCount];
        var processedMessagesByNode = new int[nodeCount];
        GraphDeterministicPartitioner.Execute(
            context.Partitions,
            _options.DegreeOfParallelism,
            partition =>
            {
                for (var nodeIndex = partition.StartIndex; nodeIndex < partition.EndIndex; nodeIndex++)
                {
                    ExecuteNode(
                        new ExecuteNodeParameters
                        {
                            Projection = context.Projection,
                            NodeIndex = nodeIndex,
                            Superstep = superstep,
                            CurrentMessages = context.CurrentMessages,
                            States = context.States,
                            Halted = context.Halted,
                            OutgoingMessagesByNode = outgoingMessagesByNode,
                            ProcessedMessagesByNode = processedMessagesByNode,
                            CancellationToken = context.CancellationToken
                        });
                }
            },
            context.CancellationToken);

        var sentMessageCount = 0L;
        for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            var outgoingMessages = outgoingMessagesByNode[nodeIndex];
            if (outgoingMessages is null)
            {
                continue;
            }

            sentMessageCount += outgoingMessages.Count;
            EnsureMessageLimit(superstep, sentMessageCount);
            Dispatch(outgoingMessages, nextMessages);
        }

        return new SuperstepResult(nextMessages, processedMessagesByNode.Sum(value => (long)value), sentMessageCount);
    }

    private void EnsureMessageLimit(int superstep, long sentMessageCount)
    {
        if (sentMessageCount > _options.MaximumMessagesPerSuperstep)
        {
            throw new GraphComputeMessageLimitException(superstep, _options.MaximumMessagesPerSuperstep);
        }
    }

    private void ReportProgress(int supersteps, bool hasActiveNode, long sentMessageCount)
    {
        if (supersteps % _options.ProgressInterval != 0 &&
            (hasActiveNode || sentMessageCount != 0) &&
            supersteps != _options.MaximumSupersteps)
        {
            return;
        }

        _options.Progress?.Report(new GraphAlgorithmProgress
        {
            Operation = "compute",
            Stage = "superstep",
            Completed = supersteps,
            Total = _options.MaximumSupersteps,
            Message = $"Completed graph compute superstep {supersteps} of {_options.MaximumSupersteps}."
        });
    }

    private static Dictionary<Guid, TState> CreateFinalStates(GraphProjection projection, TState[] states)
    {
        var finalStates = new Dictionary<Guid, TState>(states.Length);
        for (var nodeIndex = 0; nodeIndex < states.Length; nodeIndex++)
        {
            finalStates.Add(projection.GetNode(nodeIndex).Id, states[nodeIndex]);
        }

        return finalStates;
    }

    private void ExecuteNode(ExecuteNodeParameters inputs)
    {
        var projection = inputs.Projection;
        var nodeIndex = inputs.NodeIndex;
        var superstep = inputs.Superstep;
        var currentMessages = inputs.CurrentMessages;
        var states = inputs.States;
        var halted = inputs.Halted;
        var outgoingMessagesByNode = inputs.OutgoingMessagesByNode;
        var processedMessagesByNode = inputs.ProcessedMessagesByNode;
        var cancellationToken = inputs.CancellationToken;

        var messages = currentMessages[nodeIndex];
        var hasMessages = messages is { Count: > 0 };
        if (superstep > 0 && halted[nodeIndex] && !hasMessages)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        processedMessagesByNode[nodeIndex] = messages?.Count ?? 0;
        var outgoingMessages = new List<GraphComputeMessage<TMessage>>();
        var context = new GraphComputeContext<TState, TMessage>(projection, nodeIndex, states[nodeIndex], superstep, outgoingMessages);

        _program.Compute(context, messages ?? []);
        states[nodeIndex] = context.State;
        halted[nodeIndex] = context.VotedToHalt;
        outgoingMessagesByNode[nodeIndex] = outgoingMessages;
    }

    private void Dispatch(IEnumerable<GraphComputeMessage<TMessage>> outgoingMessages, List<TMessage>?[] nextMessages)
    {
        foreach (var outgoingMessage in outgoingMessages)
        {
            var targetMessages = nextMessages[outgoingMessage.TargetNodeIndex];
            if (targetMessages is null)
            {
                targetMessages = [];
                nextMessages[outgoingMessage.TargetNodeIndex] = targetMessages;
            }

            if (_messageReducer is null || targetMessages.Count == 0)
            {
                targetMessages.Add(outgoingMessage.Value);
            }
            else
            {
                targetMessages[0] = _messageReducer(targetMessages[0], outgoingMessage.Value);
            }
        }
    }

    private static void ValidateOptions(GraphComputeOptions options)
    {
        if (options.MaximumSupersteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum supersteps must be greater than zero.");
        }

        if (options.MaximumMessagesPerSuperstep <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The message limit must be greater than zero.");
        }

        if (options.DegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Degree of parallelism must be greater than zero.");
        }

        if (options.MinimumNodesPerPartition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum nodes per partition must be greater than zero.");
        }

        if (options.ProgressInterval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Progress interval must be greater than zero.");
        }
    }

    private sealed class ExecuteNodeParameters
    {
        public required GraphProjection Projection { get; init; }

        public required int NodeIndex { get; init; }

        public required int Superstep { get; init; }

        public required List<TMessage>?[] CurrentMessages { get; init; }

        public required TState[] States { get; init; }

        public required bool[] Halted { get; init; }

        public required List<GraphComputeMessage<TMessage>>?[] OutgoingMessagesByNode { get; init; }

        public required int[] ProcessedMessagesByNode { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed record SuperstepResult(List<TMessage>?[] NextMessages, long ProcessedMessages, long SentMessages);

    private sealed record SuperstepContext(
        GraphProjection Projection,
        TState[] States,
        bool[] Halted,
        List<TMessage>?[] CurrentMessages,
        IReadOnlyList<GraphWorkPartition> Partitions,
        CancellationToken CancellationToken);

    private readonly record struct ComputeExecution(int Supersteps, long ProcessedMessages, bool Converged);
}