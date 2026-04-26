namespace AgentAI.Modules.Health;

public static class HealthModule
{
    public static IServiceCollection AddHealthModule(this IServiceCollection services)
    {
        services.AddScoped<HealthService>();
        return services;
    }

    public static IEndpointRouteBuilder MapHealthModule(this IEndpointRouteBuilder app)
    {
        HealthEndpoints.Map(app);
        return app;
    }
}