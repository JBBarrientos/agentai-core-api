namespace AgentAI.Modules.KbUsages;

public static class KbUsageModule
{
    public static IServiceCollection AddKbUsageModule(this IServiceCollection services)
    {
        services.AddScoped<IKbUsageRepository, KbUsageRepository>();
        services.AddScoped<IKbUsageService, KbUsageService>();
        return services;
    }

    public static IEndpointRouteBuilder MapKbUsageModule(this IEndpointRouteBuilder app)
    {
        app.MapKbUsageEndpoints();
        return app;
    }
}