using AgentAI.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentAI.Modules.AgentRuns;

public class AgentRunRepository : IAgentRunRepository
{
    private readonly AppDbContext _db;
    public AgentRunRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<AgentRun>> GetByTicketIdAsync(int ticketId, CancellationToken ct = default)
        => await _db.AgentRuns
            .Where(r => r.TicketId == ticketId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

    public async Task<AgentRun?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(AgentRun run, CancellationToken ct = default)
    {
        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AgentRun run, CancellationToken ct = default)
    {
        _db.AgentRuns.Update(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var run = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return false;
        _db.AgentRuns.Remove(run);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}