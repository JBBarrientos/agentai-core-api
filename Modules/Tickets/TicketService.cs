using AgentAI.Modules.Queue;
using System.Text.Json;
using AgentAI.Modules.Tickets.Dto;

namespace AgentAI.Modules.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<TicketService> _logger;
    private readonly IQueueService _queueService;

    public TicketService(
        ITicketRepository repository,
        ILogger<TicketService> logger,
        [FromKeyedServices("inbound")] IQueueService queueService)
    {
        _repository = repository;
        _logger = logger;
        _queueService = queueService;
    }
    public async Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public async Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);

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
        if (req.ResolvedAt is not null) ticket.ResolvedAt = req.ResolvedAt;

        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.LastSyncedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(ticket, ct);
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

    public async Task<IEnumerable<Ticket>> GetFromServiceNowAsync(int limit, string? query, CancellationToken ct = default)
    {
        // TODO: call ServiceNow client and return results
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Ticket>> SyncFromServiceNowAsync(int limit, string? query, CancellationToken ct = default)
    {
        // TODO: fetch from ServiceNow and upsert via _repository
        throw new NotImplementedException();
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
            OpenedAt = now,
            UpdatedAt = now,
            LastSyncedAt = now
        };

        await _repository.AddAsync(ticket, ct);
        return ticket;
    }
    private async Task<string> GenerateTicketNumberAsync(CancellationToken ct = default)
    {
        var count = await _repository.CountAsync(ct);
        return $"TKT{(count + 1):D6}";
    }
}
