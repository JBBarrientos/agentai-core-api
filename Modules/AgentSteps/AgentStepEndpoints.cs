using AgentAI.Modules.AgentSteps.Dto;

namespace AgentAI.Modules.AgentSteps;

public static class AgentStepEndpoints
{
    public static IEndpointRouteBuilder MapAgentStepEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/agent-steps").WithTags("AgentSteps");

        group.MapGet("/run/{agentRunId:int}", async (int agentRunId, IAgentStepService service, CancellationToken ct) =>
            Results.Ok(await service.GetByAgentRunIdAsync(agentRunId, ct)));

        group.MapGet("/{id:int}", async (int id, IAgentStepService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } step
                ? Results.Ok(step)
                : Results.NotFound());

        group.MapPost("/", async (CreateAgentStepRequest req, IAgentStepService service, CancellationToken ct) =>
        {
            var step = await service.CreateAsync(req, ct);
            return Results.Created($"/agent-steps/{step.Id}", step);
        });

        group.MapPut("/{id:int}", async (int id, UpdateAgentStepRequest req, IAgentStepService service, CancellationToken ct) =>
            await service.UpdateAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}