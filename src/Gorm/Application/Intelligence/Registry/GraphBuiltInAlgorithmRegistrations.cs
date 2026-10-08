namespace Gorm.Application.Intelligence.Registry;

internal static class GraphBuiltInAlgorithmRegistrations
{
    public static void AddTo(GraphAlgorithmRegistry registry)
    {
        GraphCentralityRegistrations.AddTo(registry);
        GraphStructureRegistrations.AddTo(registry);
        GraphPlanningRegistrations.AddTo(registry);
        GraphCommunityRegistrations.AddTo(registry);
        GraphRoutingRegistrations.AddTo(registry);
    }
}