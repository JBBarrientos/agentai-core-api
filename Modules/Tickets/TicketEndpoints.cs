using AgentAI.Modules.Tickets.Dto;

namespace AgentAI.Modules.Tickets;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tickets")
                       .WithTags("Tickets");

        group.MapGet("/", async (ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, ITicketService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } ticket
                ? Results.Ok(ticket)
                : Results.NotFound());

        group.MapGet("/by-number/{number}", async (string number, ITicketService service, CancellationToken ct) =>
            await service.GetByNumberAsync(number, ct) is { } ticket
                ? Results.Ok(ticket)
                : Results.NotFound());

        group.MapGet("/servicenow", async (int? limit, string? query, ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetFromServiceNowAsync(limit ?? 20, query, ct)))
            .AllowAnonymous();

        group.MapPost("/sync-servicenow", async (int? limit, string? query, ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.SyncFromServiceNowAsync(limit ?? 20, query, ct)))
            .AllowAnonymous();

        group.MapPost("/", async (CreateTicketRequest req, ITicketService service, CancellationToken ct) =>
        {
            await service.CreateAsync(req, ct);
            return Results.Created();
        });

        group.MapPost("/from-agent", async (CreateAgentTicketRequest req, ITicketService service, CancellationToken ct) =>
        {
            var ticket = await service.CreateFromAgentAsync(req, ct);
            return Results.Created($"/tickets/{ticket.Id}", ticket);
        });

        group.MapPut("/{id:int}", async (int id, UpdateTicketRequest req, ITicketService service, CancellationToken ct) =>
            await service.UpdateAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, ITicketService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound());

        group.MapPost("/process", async (CreateTicketRequest req, ITicketService service, CancellationToken ct) =>
        {
            var ticket = await service.CreateAsync(req, ct);
            await service.ProcessAsync(ticket.Id, ct);
            return Results.Ok(ticket);
        });

        return app;
    }
}
