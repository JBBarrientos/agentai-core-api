namespace AgentAI.Modules.Conversations;

public static class ConversationModule
{
    public static IServiceCollection AddConversationModule(this IServiceCollection services)
    {
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationService, ConversationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapConversationModule(this IEndpointRouteBuilder app)
    {
        app.MapConversationEndpoints();
        return app;
    }
}