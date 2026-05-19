using AgentAI.Modules.Conversations.Dto;

namespace AgentAI.Modules.Conversations;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/conversations")
                       .WithTags("Conversations");

        group.MapGet("/", async (IConversationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, IConversationService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } conversation
                ? Results.Ok(conversation)
                : Results.NotFound());

        group.MapGet("/ticket/{ticketId:int}", async (int ticketId, IConversationService service, CancellationToken ct) =>
            Results.Ok(await service.GetByTicketIdAsync(ticketId, ct)));

        group.MapPost("/", async (CreateConversationRequest req, IConversationService service, CancellationToken ct) =>
        {
            await service.CreateAsync(req, ct);
            return Results.Created();
        });

        group.MapPut("/{id:int}", async (int id, UpdateConversationRequest req, IConversationService service, CancellationToken ct) =>
            await service.UpdateAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, IConversationService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}