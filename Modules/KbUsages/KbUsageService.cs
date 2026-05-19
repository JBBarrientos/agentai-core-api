using AgentAI.Modules.KbUsages.Dto;

namespace AgentAI.Modules.KbUsages;

public class KbUsageService : IKbUsageService
{
    private readonly IKbUsageRepository _repository;
    private readonly ILogger<KbUsageService> _logger;

    public KbUsageService(IKbUsageRepository repository, ILogger<KbUsageService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<KbUsageResponse>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default)
    {
        var usages = await _repository.GetByAgentRunIdAsync(agentRunId, ct);
        return usages.Select(ToResponse);
    }

    public async Task<KbUsageResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var usage = await _repository.GetByIdAsync(id, ct);
        return usage is null ? null : ToResponse(usage);
    }

    public async Task CreateAsync(CreateKbUsageRequest req, CancellationToken ct = default)
    {
        var usage = new KbUsage
        {
            AgentStepId = req.AgentStepId,
            ExternalArticleId = req.ExternalArticleId,
            ArticleTitle = req.ArticleTitle,
            ResultedInResolution = null,
            UsedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(usage, ct);
    }

    public async Task<bool> UpdateResolutionAsync(int id, UpdateKbUsageResolutionRequest req, CancellationToken ct = default)
    {
        var usage = await _repository.GetByIdAsync(id, ct);
        if (usage is null) return false;

        usage.ResultedInResolution = req.ResultedInResolution;
        await _repository.UpdateAsync(usage, ct);
        return true;
    }

    private static KbUsageResponse ToResponse(KbUsage usage) => new(
        usage.Id,
        usage.AgentStepId,
        usage.ExternalArticleId,
        usage.ArticleTitle,
        usage.ResultedInResolution,
        usage.UsedAt
    );
}