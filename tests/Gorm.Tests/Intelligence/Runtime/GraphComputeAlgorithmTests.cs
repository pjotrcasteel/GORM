using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Compute;
using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Runtime;

[TestClass]
public sealed class GraphComputeAlgorithmTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Compute_PropagatesImpactUntilGraphBecomesInactive()
    {
        var source = CreateNode("source");
        var dependency = CreateNode("dependency");
        var customer = CreateNode("customer");
        var projection = GraphProjection.Create([source, dependency, customer], [CreateEdge(source, dependency, 0.5), CreateEdge(dependency, customer, 0.5)]);

        var result = projection.Compute(
            new ImpactProgram(source.Id),
            messageReducer: (left, right) => new ImpactMessage(Math.Max(left.Impact, right.Impact)),
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(result.Converged);
        Assert.AreEqual(1, result.GetState(source.Id).Impact);
        Assert.AreEqual(0.5, result.GetState(dependency.Id).Impact);
        Assert.AreEqual(0.25, result.GetState(customer.Id).Impact);
        Assert.AreEqual(3, result.Supersteps);
        Assert.AreEqual(2, result.ProcessedMessages);
    }

    [TestMethod]
    public void Compute_ReducerCombinesMessagesBeforeTargetExecutes()
    {
        var source = CreateNode("source");
        var left = CreateNode("left");
        var right = CreateNode("right");
        var target = CreateNode("target");
        var projection = GraphProjection.Create(
            [source, left, right, target],
            [
                CreateEdge(source, left, 0.9),
                CreateEdge(source, right, 0.8),
                CreateEdge(left, target, 0.5),
                CreateEdge(right, target, 0.5)
            ]);

        var result = projection.Compute(
            new ImpactProgram(source.Id),
            messageReducer: (leftMessage, rightMessage) =>
                new ImpactMessage(Math.Max(leftMessage.Impact, rightMessage.Impact)),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0.45, result.GetState(target.Id).Impact, 0.000000001);
        Assert.AreEqual(3, result.ProcessedMessages);
    }

    [TestMethod]
    public void Compute_WhenMessageLimitIsExceeded_ThrowsSpecificException()
    {
        var source = CreateNode("source");
        var first = CreateNode("first");
        var second = CreateNode("second");
        var projection = GraphProjection.Create([source, first, second], [CreateEdge(source, first, 1), CreateEdge(source, second, 1)]);

        var exception = Assert.ThrowsExactly<GraphComputeMessageLimitException>(() => projection.Compute(
            new ImpactProgram(source.Id),
            new GraphComputeOptions
            {
                MaximumMessagesPerSuperstep = 1
            },
            cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual(0, exception.Superstep);
        Assert.AreEqual(1, exception.Limit);
    }

    [TestMethod]
    public void Compute_WithParallelSupersteps_MatchesSequentialResultExactly()
    {
        var source = CreateNode("source");
        var first = CreateNode("first");
        var second = CreateNode("second");
        var target = CreateNode("target");
        var projection = GraphProjection.Create(
            [source, first, second, target],
            [
                CreateEdge(source, first, 0.9),
                CreateEdge(source, second, 0.8),
                CreateEdge(first, target, 0.5),
                CreateEdge(second, target, 0.5)
            ]);
        static ImpactMessage reducer(ImpactMessage left, ImpactMessage right) => new(Math.Max(left.Impact, right.Impact));

        var sequential = projection.Compute(
            new ImpactProgram(source.Id),
            messageReducer: reducer,
            cancellationToken: TestContext.CancellationToken);
        var parallel = projection.Compute(
            new ImpactProgram(source.Id),
            new GraphComputeOptions { DegreeOfParallelism = 4 },
            reducer,
            TestContext.CancellationToken);

        Assert.AreEqual(sequential.Supersteps, parallel.Supersteps);
        Assert.AreEqual(sequential.ProcessedMessages, parallel.ProcessedMessages);
        Assert.AreEqual(sequential.Converged, parallel.Converged);
        foreach (var node in projection.Nodes)
        {
            Assert.AreEqual(sequential.GetState(node.Id), parallel.GetState(node.Id));
        }
    }

    [TestMethod]
    public void Compute_WithInvalidParallelism_ThrowsArgumentOutOfRangeException()
    {
        var node = CreateNode("node");
        var projection = GraphProjection.Create([node], []);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => projection.Compute(
            new ImpactProgram(node.Id),
            new GraphComputeOptions { DegreeOfParallelism = 0 },
            cancellationToken: TestContext.CancellationToken));
    }

    private static ComputeNode CreateNode(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name
    };

    private static ComputeEdge CreateEdge(ComputeNode from, ComputeNode to, double propagationFactor) => new()
    {
        Id = Guid.NewGuid(),
        FromId = from.Id,
        ToId = to.Id,
        PropagationFactor = propagationFactor
    };

    private sealed class ImpactProgram(Guid sourceNodeId) : IGraphComputeProgram<ImpactState, ImpactMessage>
    {
        public ImpactState Initialize(Node node) => new(Impact: 0);

        public void Compute(GraphComputeContext<ImpactState, ImpactMessage> context, IReadOnlyList<ImpactMessage> messages)
        {
            var receivedImpact = context.Superstep == 0 && context.Node.Id == sourceNodeId ? 1 : messages.Count == 0 ? 0 : messages.Max(message => message.Impact);

            if (receivedImpact > context.State.Impact)
            {
                context.SetState(new ImpactState(receivedImpact));
                context.SendToOutgoing((edge, _) => new ImpactMessage(receivedImpact * ((ComputeEdge)edge).PropagationFactor));
            }

            context.VoteToHalt();
        }
    }

    private readonly record struct ImpactState(double Impact);

    private readonly record struct ImpactMessage(double Impact);

    private sealed class ComputeNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ComputeEdge : Edge
    {
        public double PropagationFactor { get; set; }
    }
}