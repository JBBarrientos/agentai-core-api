namespace AgentAI.Modules.KnowledgeBase;

public static class KnowledgeBaseModule
{
    public static IServiceCollection AddKnowledgeBaseModule(this IServiceCollection services)
    {
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        return services;
    }

    public static IEndpointRouteBuilder MapKnowledgeBaseModule(this IEndpointRouteBuilder app)
    {
        app.MapKnowledgeBaseEndpoints();
        return app;
    }
}

