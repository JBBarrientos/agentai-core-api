namespace AgentAI.Modules.AuditLog;
public interface IAuditLogRepository
{
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default);
    Task<AuditLog?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}