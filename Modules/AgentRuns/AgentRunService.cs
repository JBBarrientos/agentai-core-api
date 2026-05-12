using AgentAI.Modules.AgentRuns.Dto;

namespace AgentAI.Modules.AgentRuns;

public class AgentRunService : IAgentRunService
{
    private readonly IAgentRunRepository _repository;
    private readonly ILogger<AgentRunService> _logger;

    public AgentRunService(IAgentRunRepository repository, ILogger<AgentRunService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<AgentRunResponse>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default)
    {
        var runs = await _repository.GetByTicketIdAsync(ticketId, ct);
        return runs.Select(ToResponse);
    }

    public async Task<AgentRunResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var run = await _repository.GetByIdAsync(id, ct);
        return run is null ? null : ToResponse(run);
    }

    public async Task CreateAsync(CreateAgentRunRequest req, CancellationToken ct = default)
    {
        var run = new AgentRun
        {
            TicketId = req.TicketId,
            Status = "pending",
            StartedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(run, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);

    private static AgentRunResponse ToResponse(AgentRun run) => new(
        run.Id,
        run.TicketId,
        run.Status,
        run.StartedAt,
        run.EndedAt
    );

    public async Task<bool> UpdateStatusAsync(int id, UpdateAgentRunStatusRequest req, CancellationToken ct = default)
    {
        var run = await _repository.GetByIdAsync(id, ct);
        if (run is null) return false;

        run.Status = req.Status;
        if (req.Status is "completed" or "failed")
            run.EndedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(run, ct);
        return true;
    }
}