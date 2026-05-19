namespace AgentAI.Modules.AgentSteps;

public static class AgentStepModule
{
    public static IServiceCollection AddAgentStepModule(this IServiceCollection services)
    {
        services.AddScoped<IAgentStepRepository, AgentStepRepository>();
        services.AddScoped<IAgentStepService, AgentStepService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgentStepModule(this IEndpointRouteBuilder app)
    {
        app.MapAgentStepEndpoints();
        return app;
    }
}