using AgentAI.Modules.KbUsages.Dto;

namespace AgentAI.Modules.KbUsages;

public interface IKbUsageService
{
    Task<IEnumerable<KbUsageResponse>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default);
    Task<KbUsageResponse?> GetByIdAsync(int id, CancellationToken ct = default);
    Task CreateAsync(CreateKbUsageRequest request, CancellationToken ct = default);
    Task<bool> UpdateResolutionAsync(int id, UpdateKbUsageResolutionRequest request, CancellationToken ct = default);
}