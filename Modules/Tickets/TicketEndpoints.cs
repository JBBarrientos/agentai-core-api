using AgentAI.Modules.Tickets.Dto;

namespace AgentAI.Modules.Tickets;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tickets")
                       .WithTags("Tickets");

        group.MapGet("/", async (
            string? estado,
            string? prioridad,
            DateOnly? desde,
            DateOnly? hasta,
            ITicketService service,
            CancellationToken ct) =>
        {
            var tickets = await service.GetAllAsync(ct);

            if (!string.IsNullOrWhiteSpace(estado))
                tickets = tickets.Where(t =>
                    t.StateLabel.Equals(estado, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(prioridad))
                tickets = tickets.Where(t =>
                    t.PriorityLabel.Equals(prioridad, StringComparison.OrdinalIgnoreCase));

            if (desde.HasValue)
                tickets = tickets.Where(t => DateOnly.FromDateTime(t.OpenedAt) >= desde.Value);

            if (hasta.HasValue)
                tickets = tickets.Where(t => DateOnly.FromDateTime(t.OpenedAt) <= hasta.Value);

            return Results.Ok(tickets);
        });

        group.MapGet("/escalados", async (ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetEscaladosAsync(ct)))
            .AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, ITicketService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } ticket
                ? Results.Ok(ticket)
                : Results.NotFound());

        group.MapGet("/by-number/{number}", async (string number, ITicketService service, CancellationToken ct) =>
            await service.GetByNumberAsync(number, ct) is { } ticket
                ? Results.Ok(ticket)
                : Results.NotFound())
            .AllowAnonymous();

        group.MapGet("/servicenow", async (int? limit, string? query, ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetFromServiceNowAsync(limit ?? 20, query, ct)))
            .AllowAnonymous();

        group.MapGet("/servicenow/all", async (int? pageSize, int? maxPages, string? query, ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllFromServiceNowAsync(pageSize ?? 100, maxPages ?? 50, query, ct)))
            .AllowAnonymous();

        group.MapPost("/sync-servicenow", async (int? limit, string? query, ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.SyncFromServiceNowAsync(limit ?? 20, query, ct)))
            .AllowAnonymous();

        group.MapPost("/sync-servicenow/all", async (int? pageSize, int? maxPages, string? query, ITicketService service, CancellationToken ct) =>
            Results.Ok(await service.SyncAllFromServiceNowAsync(pageSize ?? 100, maxPages ?? 50, query, ct)))
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
        })
        .AllowAnonymous();

        group.MapPut("/{id:int}", async (int id, UpdateTicketRequest req, ITicketService service, CancellationToken ct) =>
            await service.UpdateAsync(id, req, ct)
                ? Results.NoContent()
                : Results.NotFound())
            .AllowAnonymous();

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

    private static IEnumerable<Ticket> FilterTickets(
        IEnumerable<Ticket> tickets,
        string? estado,
        string? prioridad,
        string? sistema,
        DateOnly? desde,
        DateOnly? hasta)
    {
        var query = tickets;

        if (!string.IsNullOrWhiteSpace(estado) && !estado.Equals("Todos", StringComparison.OrdinalIgnoreCase))
        {
            query = estado.Trim().ToLowerInvariant() switch
            {
                "resueltos" => query.Where(IsResolved),
                "noresueltos" => query.Where(IsUnresolved),
                "escalado" or "escalados" => query.Where(IsEscalated),
                _ => query.Where(ticket => ticket.StateLabel.Equals(estado, StringComparison.OrdinalIgnoreCase))
            };
        }

        if (!string.IsNullOrWhiteSpace(prioridad) && !prioridad.Equals("Todas", StringComparison.OrdinalIgnoreCase))
            query = query.Where(ticket => ticket.PriorityLabel.Equals(prioridad, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(sistema) && !sistema.Equals("Todos", StringComparison.OrdinalIgnoreCase))
            query = query.Where(ticket => ticket.AffectedSystem.Equals(sistema, StringComparison.OrdinalIgnoreCase));

        if (desde is not null)
        {
            var from = desde.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(ticket => ticket.OpenedAt >= from);
        }

        if (hasta is not null)
        {
            var to = hasta.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(ticket => ticket.OpenedAt <= to);
        }

        return query.OrderByDescending(ticket => ticket.OpenedAt).ToList();
    }

    private static bool IsResolved(Ticket ticket)
        => ticket.State is 4 or 5 ||
            ticket.StateLabel.Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
            ticket.StateLabel.Equals("Closed", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnresolved(Ticket ticket)
        => !IsEscalated(ticket) &&
            (ticket.State is 1 or 2 or 3 ||
             ticket.StateLabel.Equals("New", StringComparison.OrdinalIgnoreCase) ||
             ticket.StateLabel.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
             ticket.StateLabel.Equals("On Hold", StringComparison.OrdinalIgnoreCase));

    private static bool IsEscalated(Ticket ticket)
        => ticket.AssignmentGroup.Equals("Soporte Nivel 2", StringComparison.OrdinalIgnoreCase);
}
