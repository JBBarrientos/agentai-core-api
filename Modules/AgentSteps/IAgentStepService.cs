using AgentAI.Modules.AgentSteps.Dto;

namespace AgentAI.Modules.AgentSteps;

public interface IAgentStepService
{
    Task<IEnumerable<AgentStepResponse>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default);
    Task<AgentStepResponse?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AgentStepResponse> CreateAsync(CreateAgentStepRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpdateAgentStepRequest request, CancellationToken ct = default);
}