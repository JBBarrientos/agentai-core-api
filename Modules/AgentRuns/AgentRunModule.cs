namespace AgentAI.Modules.AgentRuns;

public static class AgentRunModule
{
    public static IServiceCollection AddAgentRunModule(this IServiceCollection services)
    {
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
        services.AddScoped<IAgentRunService, AgentRunService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgentRunModule(this IEndpointRouteBuilder app)
    {
        app.MapAgentRunEndpoints();
        return app;
    }
}