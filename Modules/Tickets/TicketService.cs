using AgentAI.Modules.Queue;
using System.Text.Json;
using AgentAI.Modules.Tickets.Dto;
using AgentAI.Modules.ServiceNow;

namespace AgentAI.Modules.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<TicketService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IQueueService _queueService;
    private readonly IServiceNowConnector _serviceNow;

    public TicketService(
        ITicketRepository repository,
        ILogger<TicketService> logger,
        IConfiguration configuration,
        IServiceNowConnector serviceNow,
        [FromKeyedServices("inbound")] IQueueService queueService)
    {
        _repository = repository;
        _logger = logger;
        _configuration = configuration;
        _serviceNow = serviceNow;
        _queueService = queueService;
    }
    public async Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public async Task<IEnumerable<Ticket>> GetEscaladosAsync(CancellationToken ct = default)
        => await _repository.GetEscaladosAsync(ct);

    public async Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);
    public async Task<Ticket?> GetBySysIdAsync(string sysId, CancellationToken ct = default)
        => await _repository.GetBySysIdAsync(sysId, ct);
    public async Task<TicketResponse> CreateAsync(CreateTicketRequest req, CancellationToken ct = default)
    {
        var ticket = new Ticket
        {
            SysId = req.SysId,
            Number = req.Number,
            Title = req.Title,
            Description = req.Description,
            State = req.State,
            StateLabel = req.StateLabel,
            Priority = req.Priority,
            PriorityLabel = req.PriorityLabel,
            AffectedSystem = NormalizeAffectedSystem(req.AffectedSystem) ?? InferAffectedSystem($"{req.Title} {req.Description}"),
            OpenedAt = req.OpenedAt,
            UpdatedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(ticket, ct);

        return new TicketResponse(
            Id: ticket.Id,
            SysId: ticket.SysId,
            Number: ticket.Number,
            Title: ticket.Title,
            Description: ticket.Description,
            State: ticket.State,
            StateLabel: ticket.StateLabel,
            Priority: ticket.Priority,
            PriorityLabel: ticket.PriorityLabel,
            AssignmentGroup: ticket.AssignmentGroup,
            AffectedSystem: ticket.AffectedSystem,
            OpenedAt: ticket.OpenedAt,
            UpdatedAt: ticket.UpdatedAt,
            ResolvedAt: ticket.ResolvedAt,
            LastSyncedAt: ticket.LastSyncedAt
        );
    }

    public async Task<bool> UpdateAsync(int id, UpdateTicketRequest req, CancellationToken ct = default)
    {
        var ticket = await _repository.GetByIdAsync(id, ct);
        if (ticket is null) return false;

        if (req.Title is not null) ticket.Title = req.Title;
        if (req.Description is not null) ticket.Description = req.Description;
        if (req.State is not null) ticket.State = req.State.Value;
        if (req.StateLabel is not null) ticket.StateLabel = req.StateLabel;
        if (req.Priority is not null) ticket.Priority = req.Priority.Value;
        if (req.PriorityLabel is not null) ticket.PriorityLabel = req.PriorityLabel;
        if (req.AssignmentGroup is not null) ticket.AssignmentGroup = req.AssignmentGroup;
        if (req.AffectedSystem is not null) ticket.AffectedSystem = NormalizeAffectedSystem(req.AffectedSystem) ?? string.Empty;
        if (req.ResolvedAt is not null) ticket.ResolvedAt = req.ResolvedAt;

        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.LastSyncedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(ticket, ct);
        if (ticket.State == 4 && !string.IsNullOrWhiteSpace(ticket.SysId))
        {
            try
            {
                await _serviceNow.ResolveIncidentAsync(
                    ticket.SysId,
                    req.Description ?? "Ticket resuelto por AgentAI.",
                    "AgentAI marco el ticket como resuelto desde el agente de accion.",
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve ServiceNow incident for local ticket {TicketNumber}.", ticket.Number);
            }
        }

        return true;
    }
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);

    public async Task ProcessAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _repository.GetByIdAsync(ticketId, ct);
        if (ticket is null) throw new Exception($"Ticket {ticketId} not found");

        await _queueService.SendMessageAsync(JsonSerializer.Serialize(new InboundMessage(
            TicketId: ticket.Id.ToString(),
            CorrelationId: Guid.NewGuid().ToString(),
            CustomerId: ticket.SysId,
            Action: "new_ticket",
            Payload: null
        )), ct);
    }

    public async Task<Ticket?> GetByNumberAsync(string number, CancellationToken ct = default)
        => await _repository.GetByNumberAsync(number, ct);

    public async Task<IEnumerable<ServiceNowTicketResponse>> GetFromServiceNowAsync(int limit = 20, string? query = null, CancellationToken ct = default)
    {
        var incidents = await _serviceNow.GetIncidentsAsync(limit, BuildServiceNowQuery(query), ct);
        return incidents.Select(MapIncidentToServiceNowResponse);
    }

    public async Task<IEnumerable<ServiceNowTicketResponse>> GetAllFromServiceNowAsync(int pageSize = 100, int maxPages = 50, string? query = null, CancellationToken ct = default)
    {
        var incidents = await _serviceNow.GetIncidentsPagedAsync(pageSize, maxPages, BuildServiceNowQuery(query), ct);
        return incidents.Select(MapIncidentToServiceNowResponse);
    }

    public async Task<IEnumerable<Ticket>> SyncFromServiceNowAsync(int limit = 20, string? query = null, CancellationToken ct = default)
    {
        var incidents = await _serviceNow.GetIncidentsAsync(limit, BuildServiceNowQuery(query), ct);
        return await UpsertServiceNowIncidentsAsync(incidents, ct);
    }

    public async Task<IEnumerable<Ticket>> SyncAllFromServiceNowAsync(int pageSize = 100, int maxPages = 50, string? query = null, CancellationToken ct = default)
    {
        var incidents = await _serviceNow.GetIncidentsPagedAsync(pageSize, maxPages, BuildServiceNowQuery(query), ct);
        return await UpsertServiceNowIncidentsAsync(incidents, ct);
    }

    private async Task<IEnumerable<Ticket>> UpsertServiceNowIncidentsAsync(IReadOnlyList<ServiceNowIncident> incidents, CancellationToken ct)
    {
        var syncedTickets = new List<Ticket>();

        foreach (var incident in incidents)
        {
            var existing = await _repository.GetBySysIdAsync(incident.SysId, ct);
            var ticket = existing ?? new Ticket { SysId = incident.SysId };

            ApplyIncident(ticket, incident);

            if (existing is null)
                await _repository.AddAsync(ticket, ct);
            else
                await _repository.UpdateAsync(ticket, ct);

            syncedTickets.Add(ticket);
        }

        _logger.LogInformation("Synced {Count} ServiceNow incidents.", syncedTickets.Count);
        return syncedTickets;
    }

    public async Task<Ticket> SyncIncidentAsync(ServiceNowIncident incident, CancellationToken ct = default)
    {
        var existing = await _repository.GetBySysIdAsync(incident.SysId, ct);
        var ticket = existing ?? new Ticket { SysId = incident.SysId };

        ApplyIncident(ticket, incident);

        if (existing is null)
            await _repository.AddAsync(ticket, ct);
        else
            await _repository.UpdateAsync(ticket, ct);

        return ticket;
    }

    public async Task<Ticket> CreateFromAgentAsync(CreateAgentTicketRequest req, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            SysId = Guid.NewGuid().ToString("N"),
            Number = await GenerateTicketNumberAsync(ct),
            Title = $"{req.System}: {req.ErrorType}",
            Description = req.Description,
            State = 1,
            StateLabel = "New",
            Priority = 3,
            PriorityLabel = "Moderate",
            CreatedByEmail = req.UserEmail,
            AffectedSystem = NormalizeAffectedSystem(req.System) ?? InferAffectedSystem($"{req.System} {req.Description}"),
            OpenedAt = now,
            UpdatedAt = now,
            LastSyncedAt = now
        };

        await _repository.AddAsync(ticket, ct);
        return ticket;
    }

    private static Ticket MapIncidentToTicket(ServiceNowIncident incident)
    {
        var ticket = new Ticket { SysId = incident.SysId };
        ApplyIncident(ticket, incident);
        return ticket;
    }

    private static ServiceNowTicketResponse MapIncidentToServiceNowResponse(ServiceNowIncident incident)
        => new(
            incident.SysId,
            incident.Number,
            incident.Title,
            incident.Description,
            incident.State,
            incident.StateLabel,
            incident.Priority,
            incident.PriorityLabel,
            incident.CreatedByName,
            incident.CreatedByEmail,
            incident.AssignmentGroup,
            InferAffectedSystem($"{incident.Title} {incident.Description}"),
            incident.OpenedAt,
            incident.UpdatedAt,
            incident.ResolvedAt,
            DateTime.UtcNow);

    private static void ApplyIncident(Ticket ticket, ServiceNowIncident incident)
    {
        ticket.Number = incident.Number;
        ticket.Title = incident.Title;
        ticket.Description = incident.Description;
        ticket.State = incident.State;
        ticket.StateLabel = incident.StateLabel;
        ticket.Priority = incident.Priority;
        ticket.PriorityLabel = NormalizePriorityLabel(incident.PriorityLabel);
        ticket.CreatedByName = incident.CreatedByName;
        ticket.CreatedByEmail = incident.CreatedByEmail;
        ticket.AssignmentGroup = incident.AssignmentGroup;
        if (ShouldInferAffectedSystem(ticket.AffectedSystem))
            ticket.AffectedSystem = InferAffectedSystem($"{incident.Title} {incident.Description}");
        ticket.OpenedAt = incident.OpenedAt ?? DateTime.UtcNow;
        ticket.UpdatedAt = incident.UpdatedAt ?? DateTime.UtcNow;
        ticket.ResolvedAt = incident.ResolvedAt;
        ticket.LastSyncedAt = DateTime.UtcNow;
    }

    private static string NormalizePriorityLabel(string? label) => label?.Trim() switch
    {
        "Medium" => "Moderate",
        var l => l ?? string.Empty
    };

    private static bool ShouldInferAffectedSystem(string? affectedSystem)
    {
        if (string.IsNullOrWhiteSpace(affectedSystem))
            return true;

        var normalized = NormalizeForMatching(affectedSystem);
        return normalized is "turnera" or "usuarios" or "usuario";
    }

    private async Task<string> GenerateTicketNumberAsync(CancellationToken ct)
    {
        string number;

        do
        {
            number = $"INC{Random.Shared.Next(1000, 10000)}";
        }
        while (await _repository.GetByNumberAsync(number, ct) is not null);

        return number;
    }

    private string BuildServiceNowQuery(string? query)
    {
        var minIncidentNumber = _configuration["ServiceNow:MinIncidentNumber"] ?? "INC0010000";
        var baseQuery = string.IsNullOrWhiteSpace(query) ? "ORDERBYDESCsys_updated_on" : query.Trim();

        if (string.IsNullOrWhiteSpace(minIncidentNumber) ||
            baseQuery.Contains("number", StringComparison.OrdinalIgnoreCase))
        {
            return baseQuery;
        }

        return $"number>={minIncidentNumber}^{baseQuery}";
    }

    private static string InferAffectedSystem(string text)
    {
        var normalized = NormalizeForMatching(text);

        if (normalized.Contains("pago") ||
            normalized.Contains("pague") ||
            normalized.Contains("abone") ||
            normalized.Contains("credito") ||
            normalized.Contains("creditos") ||
            normalized.Contains("me dieron") ||
            normalized.Contains("me cargaron") ||
            normalized.Contains("menos clases") ||
            normalized.Contains("tarjeta") ||
            normalized.Contains("debito") ||
            normalized.Contains("cobro") ||
            normalized.Contains("cargo"))
            return "pagos";

        if (normalized.Contains("usuario") ||
            normalized.Contains("login") ||
            normalized.Contains("logue") ||
            normalized.Contains("sesion") ||
            normalized.Contains("credencial") ||
            normalized.Contains("contrasena") ||
            normalized.Contains("password") ||
            normalized.Contains("acceso"))
            return "acceso";

        if (normalized.Contains("profesor") ||
            normalized.Contains("instructor"))
            return "profesores";

        if (normalized.Contains("cupo") ||
            normalized.Contains("cupos") ||
            normalized.Contains("completo") ||
            normalized.Contains("disponibilidad") ||
            normalized.Contains("lugares"))
            return "disponibilidad";

        if (normalized.Contains("turno") ||
            normalized.Contains("reserva") ||
            normalized.Contains("turnera"))
            return "turnos";

        if (normalized.Contains("clase") ||
            normalized.Contains("horario") ||
            normalized.Contains("agenda") ||
            normalized.Contains("calendario"))
            return "clases";

        if (normalized.Contains("socio") ||
            normalized.Contains("perfil") ||
            normalized.Contains("registrado"))
            return "socios";

        if (normalized.Contains("pedido") || normalized.Contains("ord-"))
            return "pedidos";

        if (normalized.Contains("catalogo") ||
            normalized.Contains("precio") ||
            normalized.Contains("producto"))
            return "catalogo";

        if (normalized.Contains("stock") ||
            normalized.Contains("inventario") ||
            normalized.Contains("existencia"))
            return "stock";

        return "sin clasificar";
    }

    private static string? NormalizeAffectedSystem(string? value)
    {
        var normalized = NormalizeForMatching(value ?? string.Empty);
        return normalized switch
        {
            "turnera" or "turno" or "turnos" or "reserva" or "reservas" => "turnos",
            "usuarios" or "usuario" or "acceso" => "acceso",
            "socios" or "socio" => "socios",
            "profesores" or "profesor" or "instructor" => "profesores",
            "disponibilidad" or "cupos" or "cupo" => "disponibilidad",
            "clases" or "clase" => "clases",
            "pedidos" or "pedido" => "pedidos",
            "pagos" or "pago" => "pagos",
            "catalogo" or "catálogo" => "catalogo",
            "stock" => "stock",
            _ => null
        };
    }

    private static string NormalizeForMatching(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

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
