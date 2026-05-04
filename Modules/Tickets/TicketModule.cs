using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Tickets;

public static class TicketModule
{
    public static IServiceCollection AddTicketModule(this IServiceCollection services)
    {
        services.AddHttpClient<IServiceNowConnector, ServiceNowConnector>();
        services.AddHostedService<ServiceNowSyncWorker>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketService, TicketService>();

        return services;
    }

    public static IEndpointRouteBuilder MapTicketModule(this IEndpointRouteBuilder app)
    {
        app.MapTicketEndpoints();
        return app;
    }
}
