using AgentAI.Modules.AgentSteps.Dto;

namespace AgentAI.Modules.AgentSteps;

public class AgentStepService : IAgentStepService
{
    private readonly IAgentStepRepository _repository;
    private readonly ILogger<AgentStepService> _logger;

    public AgentStepService(IAgentStepRepository repository, ILogger<AgentStepService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<AgentStepResponse>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default)
    {
        var steps = await _repository.GetByAgentRunIdAsync(agentRunId, ct);
        return steps.Select(ToResponse);
    }

    public async Task<AgentStepResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var step = await _repository.GetByIdAsync(id, ct);
        return step is null ? null : ToResponse(step);
    }

    public async Task CreateAsync(CreateAgentStepRequest req, CancellationToken ct = default)
    {
        var step = new AgentStep
        {
            AgentRunId = req.AgentRunId,
            AgentType = req.AgentType,
            InputData = req.InputData,
            OutputData = string.Empty,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(step, ct);
    }

    public async Task<bool> UpdateAsync(int id, UpdateAgentStepRequest req, CancellationToken ct = default)
    {
        var step = await _repository.GetByIdAsync(id, ct);
        if (step is null) return false;

        step.Status = req.Status;
        step.OutputData = req.OutputData;

        await _repository.UpdateAsync(step, ct);
        return true;
    }

    private static AgentStepResponse ToResponse(AgentStep step) => new(
        step.Id,
        step.AgentRunId,
        step.AgentType,
        step.InputData,
        step.OutputData,
        step.Status,
        step.CreatedAt
    );
}