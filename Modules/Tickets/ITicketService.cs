using AgentAI.Modules.ServiceNow;
using AgentAI.Modules.Tickets.Dto;

namespace AgentAI.Modules.Tickets;

public interface ITicketService
{
    Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Ticket>> GetEscaladosAsync(CancellationToken ct = default);
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Ticket?> GetBySysIdAsync(string sysId, CancellationToken ct = default);
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task ProcessAsync(int ticketId, CancellationToken ct = default);
    Task<Ticket?> GetByNumberAsync(string number, CancellationToken ct = default);
    Task<IEnumerable<ServiceNowTicketResponse>> GetFromServiceNowAsync(int limit = 20, string? query = null, CancellationToken ct = default);
    Task<IEnumerable<ServiceNowTicketResponse>> GetAllFromServiceNowAsync(int pageSize = 100, int maxPages = 50, string? query = null, CancellationToken ct = default);
    Task<IEnumerable<Ticket>> SyncFromServiceNowAsync(int limit = 20, string? query = null, CancellationToken ct = default);
    Task<IEnumerable<Ticket>> SyncAllFromServiceNowAsync(int pageSize = 100, int maxPages = 50, string? query = null, CancellationToken ct = default);
    Task<Ticket> SyncIncidentAsync(ServiceNowIncident incident, CancellationToken ct = default);
    Task<Ticket> CreateFromAgentAsync(CreateAgentTicketRequest request, CancellationToken ct = default);
}
