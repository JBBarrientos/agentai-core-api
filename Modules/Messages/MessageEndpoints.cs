using AgentAI.Modules.Messages.Dto;
using AgentAI.Modules.Messages;

namespace AgentAI.Modules.Messages;

public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/messages")
                       .WithTags("Messages");

        group.MapGet("/", async (IMessageService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, IMessageService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } message
                ? Results.Ok(message)
                : Results.NotFound());

        group.MapGet("/conversation/{conversationId:int}", async (int conversationId, IMessageService service, CancellationToken ct) =>
            Results.Ok(await service.GetByConversationIdAsync(conversationId, ct)));

        group.MapPost("/", async (CreateMessageRequest req, IMessageService service, CancellationToken ct) =>
        {
            await service.CreateAsync(req, ct);
            return Results.Created();
        });

        group.MapPut("/{id:int}", async (int id, UpdateMessageRequest req, IMessageService service, CancellationToken ct) =>
            await service.UpdateAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, IMessageService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}