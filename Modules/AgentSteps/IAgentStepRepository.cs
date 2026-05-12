namespace AgentAI.Modules.AgentSteps;

public interface IAgentStepRepository
{
    Task<IEnumerable<AgentStep>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default);
    Task<AgentStep?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(AgentStep step, CancellationToken ct = default);
    Task UpdateAsync(AgentStep step, CancellationToken ct = default);
}