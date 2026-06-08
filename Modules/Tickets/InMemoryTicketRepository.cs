using System.Collections.Concurrent;

namespace AgentAI.Modules.Tickets;

public sealed class InMemoryTicketStore
{
    public ConcurrentDictionary<int, Ticket> Tickets { get; } = new();
    public int NextId;
}

public sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly InMemoryTicketStore _store;

    public InMemoryTicketRepository(InMemoryTicketStore store)
    {
        _store = store;
    }

    public Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IEnumerable<Ticket>>(_store.Tickets.Values.OrderBy(ticket => ticket.Id).ToList());

    public Task<IEnumerable<Ticket>> GetEscaladosAsync(CancellationToken ct = default)
        => Task.FromResult<IEnumerable<Ticket>>(
            _store.Tickets.Values
                .Where(t => t.StateLabel == "In Progress - Escalated")
                .OrderByDescending(t => t.UpdatedAt)
                .ToList());

    public Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _store.Tickets.TryGetValue(id, out var ticket);
        return Task.FromResult(Clone(ticket));
    }

    public Task<Ticket?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        var ticket = _store.Tickets.Values.FirstOrDefault(ticket =>
            ticket.Number.Equals(number, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(Clone(ticket));
    }

    public Task<Ticket?> GetBySysIdAsync(string sysId, CancellationToken ct = default)
    {
        var ticket = _store.Tickets.Values.FirstOrDefault(ticket =>
            ticket.SysId.Equals(sysId, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(Clone(ticket));
    }

    public Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        ticket.Id = Interlocked.Increment(ref _store.NextId);
        _store.Tickets[ticket.Id] = Clone(ticket)!;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        _store.Tickets[ticket.Id] = Clone(ticket)!;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_store.Tickets.TryRemove(id, out _));

    public Task<int> CountAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Tickets.Count);

    private static Ticket? Clone(Ticket? ticket)
    {
        if (ticket is null)
            return null;

        return new Ticket
        {
            Id = ticket.Id,
            SysId = ticket.SysId,
            Number = ticket.Number,
            Title = ticket.Title,
            Description = ticket.Description,
            State = ticket.State,
            StateLabel = ticket.StateLabel,
            Priority = ticket.Priority,
            PriorityLabel = ticket.PriorityLabel,
            CreatedByName = ticket.CreatedByName,
            CreatedByEmail = ticket.CreatedByEmail,
            AssignmentGroup = ticket.AssignmentGroup,
            AffectedSystem = ticket.AffectedSystem,
            OpenedAt = ticket.OpenedAt,
            UpdatedAt = ticket.UpdatedAt,
            ResolvedAt = ticket.ResolvedAt,
            LastSyncedAt = ticket.LastSyncedAt
        };
    }
}
