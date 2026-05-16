using AgentAI.Modules.AgentRuns.Dto;

namespace AgentAI.Modules.AgentRuns;

public interface IAgentRunService
{
    Task<IEnumerable<AgentRunResponse>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default);
    Task<AgentRunResponse?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AgentRunResponse> CreateAsync(CreateAgentRunRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(int id, UpdateAgentRunStatusRequest request, CancellationToken ct = default);

}