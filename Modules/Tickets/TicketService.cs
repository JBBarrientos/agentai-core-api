using AgentAI.Modules.Tickets.Dto;

namespace AgentAI.Modules.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<TicketService> _logger;

    public TicketService(ITicketRepository repository, ILogger<TicketService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public async Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);

    public async Task CreateAsync(CreateTicketRequest req, CancellationToken ct = default)
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

}