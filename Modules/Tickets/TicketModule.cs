using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Tickets;

public static class TicketModule
{
    public static IServiceCollection AddTicketModule(this IServiceCollection services)
    {
        services.AddTransient<ServiceNowRetryHandler>();
        services.AddHttpClient<IServiceNowConnector, ServiceNowConnector>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var timeoutSeconds = Math.Max(configuration.GetValue("ServiceNow:TimeoutSeconds", 30), 1);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .AddHttpMessageHandler<ServiceNowRetryHandler>();

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
