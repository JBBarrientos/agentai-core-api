using AgentAI.Modules.AgentRuns.Dto;

namespace AgentAI.Modules.AgentRuns;

public static class AgentRunEndpoints
{
    public static IEndpointRouteBuilder MapAgentRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/agent-runs").WithTags("AgentRuns");

        group.MapGet("/ticket/{ticketId:int}", async (int ticketId, IAgentRunService service, CancellationToken ct) =>
            Results.Ok(await service.GetByTicketIdAsync(ticketId, ct)));

        group.MapGet("/{id:int}", async (int id, IAgentRunService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } run
                ? Results.Ok(run)
                : Results.NotFound());

        group.MapPost("/", async (CreateAgentRunRequest req, IAgentRunService service, CancellationToken ct) =>
        {
            var run = await service.CreateAsync(req, ct);
            return Results.Created($"/agent-runs/{run.Id}", run);
        });

        group.MapDelete("/{id:int}", async (int id, IAgentRunService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapPut("/{id:int}/status", async (int id, UpdateAgentRunStatusRequest req, IAgentRunService service, CancellationToken ct) =>
            await service.UpdateStatusAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound());
    
        return app;
    }


}