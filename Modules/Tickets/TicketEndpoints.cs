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
            string? sistema,
            DateOnly? desde,
            DateOnly? hasta,
            string? busqueda,
            ITicketService service,
            CancellationToken ct) =>
        {
            var tickets = await service.GetAllAsync(ct);
            return Results.Ok(FilterTickets(tickets, estado, prioridad, sistema, desde, hasta, busqueda));
        })
        .AllowAnonymous();

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
        DateOnly? hasta,
        string? busqueda = null)
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
            query = query.Where(ticket => NormalizeSystem(ticket).Equals(NormalizeSystem(sistema), StringComparison.OrdinalIgnoreCase));

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

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var term = NormalizeText(busqueda);
            query = query.Where(ticket =>
                NormalizeText(ticket.Number).Contains(term) ||
                NormalizeText(ticket.Title).Contains(term) ||
                NormalizeText(ticket.StateLabel).Contains(term) ||
                NormalizeText(TraducirEstado(ticket.StateLabel)).Contains(term) ||
                NormalizeText(ticket.PriorityLabel).Contains(term) ||
                NormalizeText(TraducirPrioridad(ticket.PriorityLabel)).Contains(term));
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
        => ticket.AssignmentGroup.Equals("Soporte Nivel 2", StringComparison.OrdinalIgnoreCase) &&
           ticket.StateLabel.Equals("In Progress", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSystem(string? system)
    {
        var normalized = (system ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "turnera" or "turno" or "reserva" or "reservas" => "turnos",
            "usuarios" or "usuario" => "acceso",
            _ => normalized
        };
    }

    private static string NormalizeSystem(Ticket ticket)
    {
        var normalized = NormalizeSystem(ticket.AffectedSystem);
        if (ticket.AffectedSystem.Equals("turnera", StringComparison.OrdinalIgnoreCase) ||
            ticket.AffectedSystem.Equals("usuarios", StringComparison.OrdinalIgnoreCase) ||
            ticket.AffectedSystem.Equals("usuario", StringComparison.OrdinalIgnoreCase))
        {
            return InferSystemFromText($"{ticket.Title} {ticket.Description}") ?? normalized;
        }

        return normalized;
    }

    private static string? InferSystemFromText(string text)
    {
        var normalized = NormalizeText(text);

        if (ContainsAny(normalized, "credencial", "login", "logue", "sesion", "contrasena", "password", "acceso"))
            return "acceso";
        if (ContainsAny(normalized, "pago", "pague", "abone", "credito", "creditos", "me dieron", "me cargaron", "menos clases", "tarjeta", "debito", "cobro", "cargo"))
            return "pagos";
        if (ContainsAny(normalized, "profesor", "instructor"))
            return "profesores";
        if (ContainsAny(normalized, "cupo", "cupos", "completo", "disponibilidad", "lugares"))
            return "disponibilidad";
        if (ContainsAny(normalized, "turno", "turnos", "reserva", "reservas", "turnera"))
            return "turnos";
        if (ContainsAny(normalized, "clase", "clases", "horario", "agenda", "calendario"))
            return "clases";
        if (ContainsAny(normalized, "socio", "perfil", "registrado"))
            return "socios";

        return null;
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(text.Contains);

    private static string TraducirEstado(string label) => label.Trim() switch
    {
        "New"         => "Nuevo",
        "In Progress" => "En progreso",
        "On Hold"     => "En espera",
        "Resolved"    => "Resuelto",
        "Closed"      => "Cerrado",
        "Canceled"    => "Cancelado",
        _             => label
    };

    private static string TraducirPrioridad(string label) => label.Trim() switch
    {
        "High"     => "Alta",
        "Moderate" => "Moderada",
        "Low"      => "Baja",
        _          => label
    };

    private static string NormalizeText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
