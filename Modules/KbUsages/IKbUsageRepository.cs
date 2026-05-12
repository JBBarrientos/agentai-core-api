namespace AgentAI.Modules.KbUsages;

public interface IKbUsageRepository
{
    Task<IEnumerable<KbUsage>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default);
    Task<KbUsage?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(KbUsage usage, CancellationToken ct = default);
    Task UpdateAsync(KbUsage usage, CancellationToken ct = default);
}