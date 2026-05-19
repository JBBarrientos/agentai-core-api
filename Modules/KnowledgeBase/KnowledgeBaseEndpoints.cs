namespace AgentAI.Modules.KnowledgeBase;

public static class KnowledgeBaseEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeBaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/knowledge-base")
            .WithTags("Knowledge Base")
            .AllowAnonymous();

        group.MapGet("/search", async (
            string query,
            string? system,
            int? limit,
            IKnowledgeBaseService service,
            CancellationToken ct) =>
        {
            var results = await service.SearchAsync(query, system, limit ?? 5, ct);
            return Results.Ok(results);
        });

        return app;
    }
}

