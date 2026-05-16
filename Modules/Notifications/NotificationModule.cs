namespace AgentAI.Modules.Notifications;

public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services)
    {
        services.AddHttpClient<INotificationSender, TelegramNotificationSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<INotificationPollingStateStore, FileNotificationPollingStateStore>();
        services.AddHostedService<NotificationTicketPollingWorker>();

        return services;
    }

    public static IEndpointRouteBuilder MapNotificationModule(this IEndpointRouteBuilder app)
    {
        app.MapNotificationEndpoints();
        return app;
    }
}
