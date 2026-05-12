namespace AgentAI.Modules.AgentRuns;

public interface IAgentRunRepository
{
    Task<IEnumerable<AgentRun>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default);
    Task<AgentRun?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(AgentRun run, CancellationToken ct = default);
    Task UpdateAsync(AgentRun run, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}