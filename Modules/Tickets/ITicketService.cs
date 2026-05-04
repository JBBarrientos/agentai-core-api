using AgentAI.Modules.Tickets.Dto;

namespace AgentAI.Modules.Tickets;


public interface ITicketService
{
    Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default);
    Task CreateAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}