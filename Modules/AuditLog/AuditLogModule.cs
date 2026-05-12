namespace AgentAI.Modules.AuditLog;

public static class AuditLogModule
{
    public static IServiceCollection AddAuditLogModule(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAuditLogModule(this IEndpointRouteBuilder app)
    {
        app.MapAuditLogEndpoints();
        return app;
    }
}