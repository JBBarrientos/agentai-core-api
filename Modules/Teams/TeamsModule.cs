namespace AgentAI.Modules.Teams;

public static class TeamsModule
{
    public static IServiceCollection AddTeamsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IMicrosoftGraphClient, MicrosoftGraphClient>();
        services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();
        services.AddScoped<ITeamsNotificationSender, FakeTeamsNotificationSender>();
        services.AddSingleton<ITeamsPollingStateStore, FileTeamsPollingStateStore>();
        services.AddHostedService<TeamsTicketPollingWorker>();

        return services;
    }

    public static IEndpointRouteBuilder MapTeamsModule(this IEndpointRouteBuilder app)
    {
        app.MapTeamsEndpoints();
        return app;
    }
}
