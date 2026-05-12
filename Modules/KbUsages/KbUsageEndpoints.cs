using AgentAI.Modules.KbUsages.Dto;

namespace AgentAI.Modules.KbUsages;

public static class KbUsageEndpoints
{
    public static IEndpointRouteBuilder MapKbUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/kb-usages").WithTags("KbUsages");

        group.MapGet("/run/{agentRunId:int}", async (int agentRunId, IKbUsageService service, CancellationToken ct) =>
            Results.Ok(await service.GetByAgentRunIdAsync(agentRunId, ct)));

        group.MapGet("/{id:int}", async (int id, IKbUsageService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } usage
                ? Results.Ok(usage)
                : Results.NotFound());

        group.MapPost("/", async (CreateKbUsageRequest req, IKbUsageService service, CancellationToken ct) =>
        {
            await service.CreateAsync(req, ct);
            return Results.Created();
        });

        group.MapPatch("/{id:int}/resolution", async (int id, UpdateKbUsageResolutionRequest req, IKbUsageService service, CancellationToken ct) =>
            await service.UpdateResolutionAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}