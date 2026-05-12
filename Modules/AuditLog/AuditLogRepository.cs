using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.AuditLog;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;
    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, int entityId, CancellationToken ct = default)
        => await _db.AuditLog
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<AuditLog?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.AuditLog.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        _db.AuditLog.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var log = await _db.AuditLog.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (log is null) return false;
        _db.AuditLog.Remove(log);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}