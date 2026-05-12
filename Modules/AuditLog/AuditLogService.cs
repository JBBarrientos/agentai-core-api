using AgentAI.Modules.AuditLog.Dto;

namespace AgentAI.Modules.AuditLog;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository repository, ILogger<AuditLogService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default)
        => await _repository.GetByEntityAsync(entityType, entityId, ct);

    public async Task<AuditLog?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);

    public async Task CreateAsync(CreateAuditLogRequest req, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            EntityType = req.EntityType,
            EntityId = req.EntityId,
            Action = req.Action,
            Payload = req.Payload,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(log, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);
}