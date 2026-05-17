namespace AgentAI.Modules.Messages;

public static class MessageModule
{
    public static IServiceCollection AddMessageModule(this IServiceCollection services)
    {
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IIncomingMessageService, IncomingMessageService>();
        return services;
    }

    public static IEndpointRouteBuilder MapMessageModule(this IEndpointRouteBuilder app)
    {
        app.MapMessageEndpoints();
        return app;
    }
}