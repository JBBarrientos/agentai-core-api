using AgentAI.Modules.AuditLog.Dto;

namespace AgentAI.Modules.AuditLog;

public interface IAuditLogService
{
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default);
    Task<AuditLog?> GetByIdAsync(int id, CancellationToken ct = default);
    Task CreateAsync(CreateAuditLogRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}   