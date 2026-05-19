using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.KbUsages;

public class KbUsageRepository : IKbUsageRepository
{
    private readonly AppDbContext _db;
    public KbUsageRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<KbUsage>> GetByAgentRunIdAsync(int agentRunId, CancellationToken ct = default)
        => await _db.KbUsages
            .Where(k => k.AgentStep.AgentRunId == agentRunId)
            .OrderBy(k => k.UsedAt)
            .ToListAsync(ct);

    public async Task<KbUsage?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.KbUsages.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task AddAsync(KbUsage usage, CancellationToken ct = default)
    {
        _db.KbUsages.Add(usage);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(KbUsage usage, CancellationToken ct = default)
    {
        _db.KbUsages.Update(usage);
        await _db.SaveChangesAsync(ct);
    }
}