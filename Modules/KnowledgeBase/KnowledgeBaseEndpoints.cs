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

        group.MapPost("/diagnosticar", async (
            DiagnosticarRequest request,
            IKnowledgeBaseService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Descripcion))
                return Results.BadRequest("La descripcion es requerida.");

            var result = await service.DiagnosticarAsync(request.Sistema, request.Descripcion, ct);
            return Results.Ok(result);
        });

        return app;
    }
}

