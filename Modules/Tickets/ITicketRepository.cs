namespace AgentAI.Modules.Tickets;

public interface ITicketRepository
{
    Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Ticket?> GetByNumberAsync(string number, CancellationToken ct = default);
    Task<Ticket?> GetBySysIdAsync(string sysId, CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
