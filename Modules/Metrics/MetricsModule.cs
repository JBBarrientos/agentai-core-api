namespace AgentAI.Modules.Metrics;

public static class MetricsModule
{
    public static IEndpointRouteBuilder MapMetricsModule(this IEndpointRouteBuilder app)
    {
        app.MapMetricsEndpoints();
        return app;
    }
}
